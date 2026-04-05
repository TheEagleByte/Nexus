using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class ProjectRepository(NexusDbContext context) : IProjectRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Projects.FindAsync([id], cancellationToken);

    public async Task<List<Project>> ListBySpokeAsync(Guid spokeId, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Projects.Where(p => p.SpokeId == spokeId);
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        return await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<List<Project>> ListAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Projects.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(p => p.SpokeId == spokeId.Value);
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        return await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Project?> GetBySpokeAndExternalKeyAsync(Guid spokeId, string externalKey, CancellationToken cancellationToken = default)
        => await _context.Projects.FirstOrDefaultAsync(p => p.SpokeId == spokeId && p.ExternalKey == externalKey, cancellationToken);

    public async Task<int> CountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Projects.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(p => p.SpokeId == spokeId.Value);
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        return await query.CountAsync(cancellationToken);
    }
}
