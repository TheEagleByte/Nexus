using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id);
    Task<List<Job>> ListByProjectAsync(Guid projectId, JobStatus? status = null, int limit = 50, int offset = 0);
    Task<List<Job>> ListBySpokeAsync(Guid spokeId, JobStatus? status = null, int limit = 50, int offset = 0);
    Task<List<Job>> ListAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0);
    Task<Job> AddAsync(Job job);
    Task UpdateAsync(Job job);
    Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null);
}
