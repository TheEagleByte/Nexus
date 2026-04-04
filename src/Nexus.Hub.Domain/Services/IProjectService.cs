using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(Guid spokeId, string name, string? externalKey = null, string? summary = null);
    Task<Project?> GetProjectAsync(Guid projectId);
    Task<List<Project>> ListProjectsAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0);
    Task UpdateProjectStatusAsync(Guid projectId, ProjectStatus status);
    Task<int> GetProjectCountAsync(Guid? spokeId = null, ProjectStatus? status = null);
}
