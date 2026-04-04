using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<List<Project>> ListBySpokeAsync(Guid spokeId, ProjectStatus? status = null, int limit = 50, int offset = 0);
    Task<List<Project>> ListAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0);
    Task<Project> AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task<Project?> GetBySpokeAndExternalKeyAsync(Guid spokeId, string externalKey);
    Task<int> CountAsync(Guid? spokeId = null, ProjectStatus? status = null);
}
