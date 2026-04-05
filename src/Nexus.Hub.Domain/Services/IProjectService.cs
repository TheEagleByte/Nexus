using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(Guid spokeId, string name, string? externalKey = null, string? summary = null, CancellationToken cancellationToken = default);
    Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<List<Project>> ListProjectsAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task UpdateProjectStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken = default);
    Task<int> GetProjectCountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default);
}
