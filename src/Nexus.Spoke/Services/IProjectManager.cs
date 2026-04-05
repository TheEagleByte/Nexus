using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IProjectManager
{
    Task<ProjectInfo> CreateProjectAsync(string projectKey, string? name = null, string? summary = null);
    Task<ProjectInfo?> GetProjectAsync(string projectKey);
    Task<IReadOnlyList<ProjectInfo>> ListProjectsAsync(ProjectStatus? statusFilter = null);
    Task UpdateStatusAsync(string projectKey, ProjectStatus newStatus);
    Task SaveTicketMetadataAsync(string projectKey, TicketMetadata ticket);
    string GetProjectPath(string projectKey);
    string GetMetaPath(string projectKey);
}
