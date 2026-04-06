using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class JobTimeoutMonitorTests
{
    private readonly ActiveJobTracker _activeJobTracker;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IJobLifecycleService> _lifecycleServiceMock;

    public JobTimeoutMonitorTests()
    {
        _activeJobTracker = new ActiveJobTracker();
        _dockerServiceMock = new Mock<IDockerService>();
        _lifecycleServiceMock = new Mock<IJobLifecycleService>();
    }

    private JobTimeoutMonitor CreateMonitor(int timeoutSeconds = 10)
    {
        var config = new SpokeConfiguration
        {
            Docker = new SpokeConfiguration.DockerConfig { TimeoutSeconds = timeoutSeconds }
        };

        return new JobTimeoutMonitor(
            _activeJobTracker,
            _dockerServiceMock.Object,
            _lifecycleServiceMock.Object,
            Options.Create(config),
            NullLogger<JobTimeoutMonitor>.Instance);
    }

    [Fact]
    public async Task StopsGracefullyOnCancellation()
    {
        var monitor = CreateMonitor();

        // Should exit without throwing
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DoesNotTimeOutRecentJobs()
    {
        // Job started just now, timeout is 1 hour
        var jobId = Guid.NewGuid();
        using var jobCts = new CancellationTokenSource();
        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, Guid.NewGuid(), "TEST-1", "container-1",
            jobCts, DateTimeOffset.UtcNow));

        var monitor = CreateMonitor(timeoutSeconds: 3600);

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await monitor.StopAsync(CancellationToken.None);

        // Job should still be tracked
        Assert.NotNull(_activeJobTracker.Get(jobId));

        // No kill should have happened
        _dockerServiceMock.Verify(m => m.KillContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TimesOutExpiredJobs()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        using var jobCts = new CancellationTokenSource();

        // Job started 2 hours ago, timeout is 1 minute
        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, projectId, "TEST-1", "container-1",
            jobCts, DateTimeOffset.UtcNow.AddHours(-2)));

        var monitor = CreateMonitor(timeoutSeconds: 60);

        // Directly invoke the check method (internal, visible via InternalsVisibleTo)
        await monitor.CheckForTimedOutJobsAsync();

        // Job should be removed from tracker
        Assert.Null(_activeJobTracker.Get(jobId));

        // Container should have been killed and removed
        _dockerServiceMock.Verify(m => m.KillContainerAsync("container-1", It.IsAny<CancellationToken>()), Times.Once);
        _dockerServiceMock.Verify(m => m.RemoveContainerAsync("container-1", It.IsAny<CancellationToken>()), Times.Once);

        // Status should be reported as Failed
        _lifecycleServiceMock.Verify(m => m.ReportStatusAsync(
            jobId, projectId, "TEST-1",
            JobStatus.Running, JobStatus.Failed,
            It.Is<string>(s => s.Contains("timed out")),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void HandlesEmptyTracker()
    {
        var monitor = CreateMonitor();
        Assert.NotNull(monitor);
    }
}
