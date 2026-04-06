using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class JobLifecycleServiceTests
{
    private readonly Mock<IHubConnectionService> _hubConnectionMock;
    private readonly Mock<IJobArtifactService> _jobArtifactsMock;
    private readonly JobLifecycleService _sut;

    public JobLifecycleServiceTests()
    {
        _hubConnectionMock = new Mock<IHubConnectionService>();
        _jobArtifactsMock = new Mock<IJobArtifactService>();
        _hubConnectionMock.Setup(h => h.SpokeId).Returns(Guid.NewGuid());

        _sut = new JobLifecycleService(
            _hubConnectionMock.Object,
            _jobArtifactsMock.Object,
            NullLogger<JobLifecycleService>.Instance);
    }

    [Fact]
    public async Task ReportStatusAsync_WritesLocalStatus()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await _sut.ReportStatusAsync(jobId, projectId, "TEST-1",
            JobStatus.Queued, JobStatus.Running);

        _jobArtifactsMock.Verify(m => m.WriteStatusAsync("TEST-1", jobId, JobStatus.Running, null), Times.Once);
    }

    [Fact]
    public async Task ReportStatusAsync_SendsEventToHub()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await _sut.ReportStatusAsync(jobId, projectId, "TEST-1",
            JobStatus.Running, JobStatus.Completed, "Done");

        _hubConnectionMock.Verify(m => m.SendAsync(
            "ReportJobStatusChanged",
            It.Is<JobStatusChangedEvent>(e =>
                e.JobId == jobId &&
                e.ProjectId == projectId &&
                e.NewStatus == JobStatus.Completed &&
                e.PreviousStatus == JobStatus.Running &&
                e.Summary == "Done"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportStatusAsync_IncludesSpokeId()
    {
        var spokeId = Guid.NewGuid();
        _hubConnectionMock.Setup(h => h.SpokeId).Returns(spokeId);

        await _sut.ReportStatusAsync(Guid.NewGuid(), Guid.NewGuid(), "TEST-1",
            JobStatus.Queued, JobStatus.Running);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "ReportJobStatusChanged",
            It.Is<JobStatusChangedEvent>(e => e.SpokeId == spokeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportStatusAsync_ContinuesWhenHubSendFails()
    {
        _hubConnectionMock.Setup(m => m.SendAsync(
                It.IsAny<string>(), It.IsAny<JobStatusChangedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        // Should not throw
        await _sut.ReportStatusAsync(Guid.NewGuid(), Guid.NewGuid(), "TEST-1",
            JobStatus.Queued, JobStatus.Running);

        // Local status should still be written
        _jobArtifactsMock.Verify(m => m.WriteStatusAsync("TEST-1", It.IsAny<Guid>(), JobStatus.Running, null), Times.Once);
    }
}
