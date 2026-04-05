using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class JobRepository(NexusDbContext context) : IJobRepository
{
    private readonly NexusDbContext _context = context;

    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Job>> ListByProjectAsync(Guid projectId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Job>> ListBySpokeAsync(Guid spokeId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Job>> ListAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
