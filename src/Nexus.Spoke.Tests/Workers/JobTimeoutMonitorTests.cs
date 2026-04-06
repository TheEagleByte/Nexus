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
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Should exit without throwing
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DoesNotTimeOutRecentJobs()
    {
        // Job started just now, timeout is 10 seconds
        var jobId = Guid.NewGuid();
        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, Guid.NewGuid(), "TEST-1", "container-1",
            new CancellationTokenSource(), DateTimeOffset.UtcNow));

        var monitor = CreateMonitor(timeoutSeconds: 3600); // 1 hour timeout

        // Start and let it run one check cycle
        using var cts = new CancellationTokenSource();
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await monitor.StopAsync(CancellationToken.None);

        // Job should still be tracked
        Assert.NotNull(_activeJobTracker.Get(jobId));

        // No kill should have happened
        _dockerServiceMock.Verify(m => m.KillContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void HandlesEmptyTracker()
    {
        // Just verify construction doesn't throw with empty tracker
        var monitor = CreateMonitor();
        Assert.NotNull(monitor);
    }
}
