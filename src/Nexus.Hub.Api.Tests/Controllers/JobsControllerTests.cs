using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class JobsControllerTests
{
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<IHubContext<NexusHub>> _hubContextMock = new();
    private readonly Mock<ILogger<JobsController>> _loggerMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        _controller = new JobsController(_jobServiceMock.Object, _projectServiceMock.Object, _hubContextMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidRequest_Returns201WithLocation()
    {
        var projectId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _projectServiceMock
            .Setup(s => s.GetProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = projectId, SpokeId = spokeId, Name = "test-project" });

        _jobServiceMock
            .Setup(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Job
            {
                Id = jobId,
                ProjectId = projectId,
                SpokeId = spokeId,
                Type = JobType.Implement,
                Status = JobStatus.Queued,
                CreatedAt = now
            });

        var request = new CreateJobRequest
        {
            ProjectId = projectId,
            Type = JobType.Implement,
            RequiresApproval = false
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal($"/api/jobs/{jobId}", created.Location);
        var response = Assert.IsType<JobResponse>(created.Value);
        Assert.Equal(jobId, response.Id);
        Assert.Equal(projectId, response.ProjectId);
        Assert.Equal(spokeId, response.SpokeId);
        Assert.Equal(JobType.Implement, response.Type);
        Assert.Equal(JobStatus.Queued, response.Status);
    }

    [Fact]
    public async Task CreateAsync_EmptyProjectId_Returns400()
    {
        var request = new CreateJobRequest
        {
            ProjectId = Guid.Empty,
            Type = JobType.Implement
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    #endregion

    #region GetAsync

    [Fact]
    public async Task GetAsync_ReturnsJobDetailResponseWithOutputCount()
    {
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _jobServiceMock
            .Setup(s => s.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Job
            {
                Id = jobId,
                ProjectId = Guid.NewGuid(),
                SpokeId = Guid.NewGuid(),
                Type = JobType.Test,
                Status = JobStatus.Running,
                CreatedAt = now,
                StartedAt = now
            });

        _jobServiceMock
            .Setup(s => s.GetJobOutputCountAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _controller.GetAsync(jobId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<JobDetailResponse>(ok.Value);
        Assert.Equal(jobId, response.Id);
        Assert.Equal(42, response.OutputChunkCount);
    }

    #endregion

    #region ApproveAsync

    [Fact]
    public async Task ApproveAsync_Returns200()
    {
        var jobId = Guid.NewGuid();

        _jobServiceMock
            .Setup(s => s.ApproveJobAsync(jobId, true, null, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ApproveJobRequest { Approved = true };

        var result = await _controller.ApproveAsync(jobId, request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        _jobServiceMock.Verify(s => s.ApproveJobAsync(jobId, true, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_Returns202()
    {
        var jobId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _jobServiceMock
            .Setup(s => s.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Job { Id = jobId, SpokeId = spokeId, Type = JobType.Implement, Status = JobStatus.Running });

        _jobServiceMock
            .Setup(s => s.CancelJobAsync(jobId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CancelJobRequest { Reason = "no longer needed" };

        var result = await _controller.CancelAsync(jobId, request, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        _jobServiceMock.Verify(s => s.CancelJobAsync(jobId, "no longer needed", It.IsAny<CancellationToken>()), Times.Once);
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("JobCancelled", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region RecordOutputAsync

    [Fact]
    public async Task RecordOutputAsync_ValidContent_Returns202()
    {
        var jobId = Guid.NewGuid();

        _jobServiceMock
            .Setup(s => s.RecordJobOutputAsync(jobId, "hello world", "stdout", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputStream { Id = Guid.NewGuid(), JobId = jobId, Content = "hello world", Sequence = 1 });

        var request = new RecordOutputRequest { Content = "hello world", StreamType = "stdout" };

        var result = await _controller.RecordOutputAsync(jobId, request, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        _jobServiceMock.Verify(
            s => s.RecordJobOutputAsync(jobId, "hello world", "stdout", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordOutputAsync_EmptyContent_Returns400()
    {
        var jobId = Guid.NewGuid();

        var request = new RecordOutputRequest { Content = "", StreamType = "stdout" };

        var result = await _controller.RecordOutputAsync(jobId, request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    #endregion

    #region GetOutputAsync

    [Fact]
    public async Task GetOutputAsync_ReturnsPaginatedOutput()
    {
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _jobServiceMock
            .Setup(s => s.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Job
            {
                Id = jobId,
                SpokeId = Guid.NewGuid(),
                Type = JobType.Implement,
                Status = JobStatus.Completed,
                CreatedAt = now,
                CompletedAt = now
            });

        _jobServiceMock
            .Setup(s => s.GetJobOutputAsync(jobId, 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutputStream>
            {
                new() { Id = Guid.NewGuid(), JobId = jobId, Sequence = 1, Content = "line 1", StreamType = "stdout", Timestamp = now },
                new() { Id = Guid.NewGuid(), JobId = jobId, Sequence = 2, Content = "line 2", StreamType = "stdout", Timestamp = now }
            });

        _jobServiceMock
            .Setup(s => s.GetJobOutputCountAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _controller.GetOutputAsync(jobId, limit: 10, offset: 0, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<JobOutputResponse>(ok.Value);
        Assert.Equal(jobId, response.JobId);
        Assert.Equal(2, response.Chunks.Count);
        Assert.Equal(5, response.TotalChunks);
        Assert.Equal(10, response.Limit);
        Assert.Equal(0, response.Offset);
        Assert.True(response.IsComplete);
    }

    [Fact]
    public async Task GetOutputAsync_NegativeOffset_Returns400()
    {
        var jobId = Guid.NewGuid();

        var result = await _controller.GetOutputAsync(jobId, limit: 10, offset: -1, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task GetOutputAsync_ZeroLimit_Returns400()
    {
        var jobId = Guid.NewGuid();

        var result = await _controller.GetOutputAsync(jobId, limit: 0, offset: 0, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    #endregion
}
