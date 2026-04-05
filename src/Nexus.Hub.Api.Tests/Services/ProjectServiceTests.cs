using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> _repo = new();
    private readonly Mock<ISpokeRepository> _spokeRepo = new();
    private readonly Mock<ILogger<ProjectService>> _logger = new();
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(_repo.Object, _spokeRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateProjectAsync_ValidSpoke_CreatesProject()
    {
        var spokeId = Guid.NewGuid();
        _spokeRepo.Setup(r => r.GetByIdAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke { Id = spokeId, Name = "test" });
        _repo.Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await _sut.CreateProjectAsync(spokeId, "Test Project", "EXT-1", "A summary");

        Assert.Equal(spokeId, result.SpokeId);
        Assert.Equal("Test Project", result.Name);
        Assert.Equal("EXT-1", result.ExternalKey);
        Assert.Equal("A summary", result.Summary);
        Assert.Equal(ProjectStatus.Planning, result.Status);
        _repo.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProjectAsync_NonExistentSpoke_ThrowsNotFoundException()
    {
        _spokeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.CreateProjectAsync(Guid.NewGuid(), "test"));
    }

    [Fact]
    public async Task GetProjectAsync_ExistingProject_ReturnsProject()
    {
        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "test", Status = ProjectStatus.Active };
        _repo.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _sut.GetProjectAsync(projectId);

        Assert.Equal(projectId, result!.Id);
    }

    [Fact]
    public async Task GetProjectAsync_NonExistent_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Project?)null);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.GetProjectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListProjectsAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        var projects = new List<Project> { new() { Id = Guid.NewGuid(), Name = "test" } };
        _repo.Setup(r => r.ListAsync(spokeId, null, 50, 0, It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var result = await _sut.ListProjectsAsync(spokeId);

        Assert.Single(result);
    }

    [Fact]
    public async Task UpdateProjectStatusAsync_NonExistentProject_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.UpdateProjectStatusAsync(Guid.NewGuid(), ProjectStatus.Active));
    }

    [Fact]
    public async Task GetProjectCountAsync_DelegatesToRepository()
    {
        _repo.Setup(r => r.CountAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await _sut.GetProjectCountAsync();

        Assert.Equal(3, result);
    }
}
