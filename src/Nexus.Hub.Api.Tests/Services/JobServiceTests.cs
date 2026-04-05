using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class JobServiceTests
{
    private readonly Mock<IJobRepository> _jobRepo = new();
    private readonly Mock<IOutputStreamRepository> _outputRepo = new();
    private readonly Mock<ILogger<JobService>> _logger = new();
    private readonly JobService _sut;

    public JobServiceTests()
    {
        _sut = new JobService(_jobRepo.Object, _outputRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateJobAsync_NoApproval_StatusIsQueued()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var result = await _sut.CreateJobAsync(spokeId, projectId, JobType.Implement);

        Assert.Equal(JobStatus.Queued, result.Status);
        Assert.Equal(spokeId, result.SpokeId);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(JobType.Implement, result.Type);
        Assert.False(result.ApprovalRequired);
        _jobRepo.Verify(r => r.AddAsync(It.IsAny<Job>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateJobAsync_RequiresApproval_StatusIsAwaitingApproval()
    {
        var result = await _sut.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), JobType.Test, requiresApproval: true);

        Assert.Equal(JobStatus.AwaitingApproval, result.Status);
        Assert.True(result.ApprovalRequired);
    }

    [Fact]
    public async Task CreateJobAsync_WithContext_SetsSummary()
    {
        var context = JsonDocument.Parse("{\"task\":\"build feature\"}");

        var result = await _sut.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), JobType.Custom, context: context);

        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task CreateJobAsync_GeneratesUniqueId()
    {
        var job1 = await _sut.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), JobType.Implement);
        var job2 = await _sut.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), JobType.Implement);

        Assert.NotEqual(job1.Id, job2.Id);
    }

    [Fact]
    public async Task GetJobAsync_ExistingJob_ReturnsJob()
    {
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Status = JobStatus.Queued, Type = JobType.Implement, CreatedAt = DateTimeOffset.UtcNow };
        _jobRepo.Setup(r => r.GetByIdAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var result = await _sut.GetJobAsync(jobId);

        Assert.Equal(jobId, result!.Id);
    }

    [Fact]
    public async Task GetJobAsync_MissingJob_ThrowsNotFoundException()
    {
        _jobRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Job?)null);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.GetJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListJobsAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        var jobs = new List<Job> { new() { Id = Guid.NewGuid(), Status = JobStatus.Queued, CreatedAt = DateTimeOffset.UtcNow } };
        _jobRepo.Setup(r => r.ListAsync(spokeId, null, JobStatus.Queued, null, 50, 0, It.IsAny<CancellationToken>())).ReturnsAsync(jobs);

        var result = await _sut.ListJobsAsync(spokeId: spokeId, status: JobStatus.Queued);

        Assert.Single(result);
        _jobRepo.Verify(r => r.ListAsync(spokeId, null, JobStatus.Queued, null, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveJobAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.ApproveJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelJobAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.CancelJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RecordJobOutputAsync_DelegatesToRepository()
    {
        var jobId = Guid.NewGuid();
        var outputStream = new OutputStream { Id = Guid.NewGuid(), JobId = jobId, Sequence = 0, Content = "output", Timestamp = DateTimeOffset.UtcNow };
        _outputRepo
            .Setup(r => r.AddWithAutoSequenceAsync(jobId, "output", "stdout", default))
            .ReturnsAsync(outputStream);

        var result = await _sut.RecordJobOutputAsync(jobId, "output");

        Assert.Equal(outputStream.Id, result.Id);
        _outputRepo.Verify(r => r.AddWithAutoSequenceAsync(jobId, "output", "stdout", default), Times.Once);
    }

    [Fact]
    public async Task GetJobCountAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        _jobRepo.Setup(r => r.CountAsync(spokeId, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var result = await _sut.GetJobCountAsync(spokeId: spokeId);

        Assert.Equal(5, result);
    }
}
