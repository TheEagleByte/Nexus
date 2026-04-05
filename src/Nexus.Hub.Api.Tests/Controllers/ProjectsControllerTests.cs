using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class ProjectsControllerTests
{
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<ILogger<ProjectsController>> _loggerMock = new();
    private readonly ProjectsController _controller;

    public ProjectsControllerTests()
    {
        _controller = new ProjectsController(_projectServiceMock.Object, _spokeServiceMock.Object, _jobServiceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ==========================================================================
    // CreateAsync
    // ==========================================================================

    [Fact]
    public async Task CreateAsync_ValidRequest_Returns201WithProjectResponse()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.CreateProjectAsync(
                spokeId,
                "test-project",
                "EXT-001",
                "A test project",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = projectId,
                SpokeId = spokeId,
                ExternalKey = "EXT-001",
                Name = "test-project",
                Summary = "A test project",
                Status = ProjectStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });

        var request = new CreateProjectRequest
        {
            SpokeId = spokeId,
            Name = "test-project",
            ExternalKey = "EXT-001",
            Summary = "A test project"
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var response = Assert.IsType<ProjectResponse>(createdResult.Value);
        Assert.Equal(projectId, response.Id);
        Assert.Equal(spokeId, response.SpokeId);
        Assert.Equal("test-project", response.Name);
        Assert.Equal("EXT-001", response.ExternalKey);
        Assert.Equal("A test project", response.Summary);
        Assert.Equal(ProjectStatus.Active, response.Status);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_Returns400()
    {
        var request = new CreateProjectRequest
        {
            SpokeId = Guid.NewGuid(),
            Name = ""
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_EmptySpokeId_Returns400()
    {
        var request = new CreateProjectRequest
        {
            SpokeId = Guid.Empty,
            Name = "test-project"
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    // ==========================================================================
    // GetAsync
    // ==========================================================================

    [Fact]
    public async Task GetAsync_ValidRequest_Returns200WithFullProjectResponse()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.GetProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project
            {
                Id = projectId,
                SpokeId = spokeId,
                ExternalKey = "EXT-001",
                Name = "test-project",
                Summary = "A test project",
                Status = ProjectStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });

        _spokeServiceMock.Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke { Id = spokeId, Name = "test-spoke" });
        _jobServiceMock.Setup(s => s.GetJobCountAsync(null, projectId, JobStatus.Running, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _jobServiceMock.Setup(s => s.GetJobCountAsync(null, projectId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _controller.GetAsync(projectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectResponse>(okResult.Value);
        Assert.Equal(projectId, response.Id);
        Assert.Equal(spokeId, response.SpokeId);
        Assert.Equal("test-spoke", response.SpokeName);
        Assert.Equal("test-project", response.Name);
        Assert.Equal(ProjectStatus.Active, response.Status);
        Assert.Equal(2, response.ActiveJobCount);
        Assert.Equal(5, response.TotalJobCount);
    }

    [Fact]
    public async Task GetAsync_NonExistentProject_ThrowsNotFoundException()
    {
        var projectId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.GetProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Nexus.Hub.Domain.Exceptions.NotFoundException($"Project {projectId} not found"));

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _controller.GetAsync(projectId, CancellationToken.None));
    }

    // ==========================================================================
    // UpdateStatusAsync
    // ==========================================================================

    [Fact]
    public async Task UpdateStatusAsync_ValidRequest_Returns200()
    {
        var projectId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Paused, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectStatusRequest { Status = ProjectStatus.Paused };

        var result = await _controller.UpdateStatusAsync(projectId, request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        _projectServiceMock.Verify(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Paused, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================================================
    // DeleteAsync
    // ==========================================================================

    [Fact]
    public async Task DeleteAsync_Returns204()
    {
        var projectId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Archived, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteAsync(projectId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAsync_CallsUpdateProjectStatusWithArchived()
    {
        var projectId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Archived, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.DeleteAsync(projectId, CancellationToken.None);

        _projectServiceMock.Verify(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Archived, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================================================
    // ListJobsByProjectAsync
    // ==========================================================================

    [Fact]
    public async Task ListJobsByProjectAsync_ReturnsJobsWithPagination()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.GetProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = projectId, SpokeId = spokeId, Name = "test-project" });

        var jobs = new List<Job>
        {
            new()
            {
                Id = Guid.NewGuid(), ProjectId = projectId, SpokeId = spokeId,
                Type = JobType.Implement, Status = JobStatus.Running,
                CreatedAt = now, Summary = "Job 1"
            },
            new()
            {
                Id = Guid.NewGuid(), ProjectId = projectId, SpokeId = spokeId,
                Type = JobType.Implement, Status = JobStatus.Completed,
                CreatedAt = now, Summary = "Job 2"
            }
        };

        _jobServiceMock
            .Setup(s => s.ListJobsAsync(null, projectId, null, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);
        _jobServiceMock
            .Setup(s => s.GetJobCountAsync(null, projectId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _controller.ListJobsByProjectAsync(projectId, limit: 50, offset: 0, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<JobListResponse>(okResult.Value);
        Assert.Equal(2, response.Jobs.Count);
        Assert.Equal(2, response.Total);
        Assert.Equal(50, response.Limit);
        Assert.Equal(0, response.Offset);
    }

    [Fact]
    public async Task ListJobsByProjectAsync_NegativeOffset_Returns400()
    {
        var result = await _controller.ListJobsByProjectAsync(Guid.NewGuid(), limit: 50, offset: -1, cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ListJobsByProjectAsync_ZeroLimit_Returns400()
    {
        var result = await _controller.ListJobsByProjectAsync(Guid.NewGuid(), limit: 0, offset: 0, cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ListJobsByProjectAsync_LimitCappedAt100()
    {
        var projectId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _projectServiceMock
            .Setup(s => s.GetProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = projectId, SpokeId = spokeId, Name = "test-project" });

        _jobServiceMock
            .Setup(s => s.ListJobsAsync(null, projectId, null, null, null, null, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _jobServiceMock
            .Setup(s => s.GetJobCountAsync(null, projectId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListJobsByProjectAsync(projectId, limit: 500, offset: 0, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<JobListResponse>(okResult.Value);
        Assert.Equal(100, response.Limit);

        _jobServiceMock.Verify(s => s.ListJobsAsync(null, projectId, null, null, null, null, 100, 0, It.IsAny<CancellationToken>()), Times.Once);
    }
}
