using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Project>> ListBySpokeAsync(Guid spokeId, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<Project>> ListAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project?> GetBySpokeAndExternalKeyAsync(Guid spokeId, string externalKey, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default);
}
