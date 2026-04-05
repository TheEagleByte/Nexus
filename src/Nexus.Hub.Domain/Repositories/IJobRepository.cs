using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Job>> ListByProjectAsync(Guid projectId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<Job>> ListBySpokeAsync(Guid spokeId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<Job>> ListAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, DateTimeOffset? from = null, DateTimeOffset? to = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
}
