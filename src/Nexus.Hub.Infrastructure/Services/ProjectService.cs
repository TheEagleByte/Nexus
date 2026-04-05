using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class ProjectService(IProjectRepository projectRepository, ILogger<ProjectService> logger) : IProjectService
{
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly ILogger<ProjectService> _logger = logger;

    public Task<Project> CreateProjectAsync(Guid spokeId, string name, string? externalKey = null, string? summary = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Project>> ListProjectsAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateProjectStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetProjectCountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
