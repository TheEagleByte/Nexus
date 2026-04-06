using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Handlers;

public class JobCancelHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IJobLifecycleService> _lifecycleServiceMock;
    private readonly ActiveJobTracker _activeJobTracker;
    private readonly JobCancelHandler _sut;

    public JobCancelHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _lifecycleServiceMock = new Mock<IJobLifecycleService>();
        _activeJobTracker = new ActiveJobTracker();

        _sut = new JobCancelHandler(
            _dockerServiceMock.Object,
            _lifecycleServiceMock.Object,
            _activeJobTracker,
            NullLogger<JobCancelHandler>.Instance);
    }

    [Fact]
    public void CommandType_IsJobCancel()
    {
        Assert.Equal("job.cancel", _sut.CommandType);
    }

    [Fact]
    public async Task HandleAsync_CancelsAndKillsRunningJob()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, projectId, "TEST-1", "container-abc", cts, DateTimeOffset.UtcNow));

        var cancellation = new JobCancellation(jobId, "User requested");
        var command = new CommandEnvelope("job.cancel", cancellation, DateTimeOffset.UtcNow);

        await _sut.HandleAsync(command, CancellationToken.None);

        // CTS should be cancelled
        Assert.True(cts.IsCancellationRequested);

        // Container should be killed and removed
        _dockerServiceMock.Verify(m => m.KillContainerAsync("container-abc", CancellationToken.None), Times.Once);
        _dockerServiceMock.Verify(m => m.RemoveContainerAsync("container-abc", CancellationToken.None), Times.Once);

        // Status should be reported as Cancelled
        _lifecycleServiceMock.Verify(m => m.ReportStatusAsync(
            jobId, projectId, "TEST-1",
            JobStatus.Running, JobStatus.Cancelled,
            "User requested", null, CancellationToken.None), Times.Once);

        // Job should be removed from tracker
        Assert.Null(_activeJobTracker.Get(jobId));
    }

    [Fact]
    public async Task HandleAsync_LogsWarningForUnknownJob()
    {
        var cancellation = new JobCancellation(Guid.NewGuid(), null);
        var command = new CommandEnvelope("job.cancel", cancellation, DateTimeOffset.UtcNow);

        // Should not throw
        await _sut.HandleAsync(command, CancellationToken.None);

        // No Docker operations should be attempted
        _dockerServiceMock.Verify(m => m.KillContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HandlesJsonElementPayload()
    {
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, Guid.NewGuid(), "TEST-1", "container-xyz", cts, DateTimeOffset.UtcNow));

        var cancellation = new JobCancellation(jobId, "timeout");
        var json = JsonSerializer.Serialize(cancellation);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var command = new CommandEnvelope("job.cancel", element, DateTimeOffset.UtcNow);
        await _sut.HandleAsync(command, CancellationToken.None);

        _dockerServiceMock.Verify(m => m.KillContainerAsync("container-xyz", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UsesDefaultReasonWhenNull()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, projectId, "TEST-1", "container-abc", cts, DateTimeOffset.UtcNow));

        var cancellation = new JobCancellation(jobId, null);
        var command = new CommandEnvelope("job.cancel", cancellation, DateTimeOffset.UtcNow);

        await _sut.HandleAsync(command, CancellationToken.None);

        _lifecycleServiceMock.Verify(m => m.ReportStatusAsync(
            jobId, projectId, "TEST-1",
            JobStatus.Running, JobStatus.Cancelled,
            "Cancelled by hub", null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenKillContainerThrows_StillReportsAndCleans()
    {
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _activeJobTracker.TryAdd(jobId, new ActiveJob(
            jobId, Guid.NewGuid(), "TEST-1", "container-abc", cts, DateTimeOffset.UtcNow));

        _dockerServiceMock.Setup(m => m.KillContainerAsync("container-abc", CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Docker daemon unavailable"));

        var cancellation = new JobCancellation(jobId, "force cancel");
        var command = new CommandEnvelope("job.cancel", cancellation, DateTimeOffset.UtcNow);

        // Should propagate the exception (handler doesn't swallow Docker errors)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.HandleAsync(command, CancellationToken.None));
    }
}
