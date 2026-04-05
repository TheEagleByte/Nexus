using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> _repo = new();
    private readonly Mock<ILogger<ProjectService>> _logger = new();
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateProjectAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.CreateProjectAsync(Guid.NewGuid(), "test"));
    }

    [Fact]
    public async Task GetProjectAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.GetProjectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListProjectsAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.ListProjectsAsync());
    }

    [Fact]
    public async Task UpdateProjectStatusAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.UpdateProjectStatusAsync(Guid.NewGuid(), ProjectStatus.Active));
    }

    [Fact]
    public async Task GetProjectCountAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.GetProjectCountAsync());
    }
}
