using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class JobRepository(NexusDbContext context) : IJobRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Jobs.FindAsync([id], cancellationToken);

    public async Task<List<Job>> ListByProjectAsync(Guid projectId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.Where(j => j.ProjectId == projectId);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        return await query.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<List<Job>> ListBySpokeAsync(Guid spokeId, JobStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.Where(j => j.SpokeId == spokeId);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        return await query.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<List<Job>> ListAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(j => j.SpokeId == spokeId.Value);
        if (projectId.HasValue)
            query = query.Where(j => j.ProjectId == projectId.Value);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        if (type.HasValue)
            query = query.Where(j => j.Type == type.Value);
        return await query.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(j => j.SpokeId == spokeId.Value);
        if (projectId.HasValue)
            query = query.Where(j => j.ProjectId == projectId.Value);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        return await query.CountAsync(cancellationToken);
    }
}
