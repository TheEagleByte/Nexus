using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class PendingActionRepository(NexusDbContext context) : IPendingActionRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.PendingActions
            .Include(pa => pa.Spoke)
            .Include(pa => pa.Project)
            .Include(pa => pa.Job)
            .FirstOrDefaultAsync(pa => pa.Id == id, cancellationToken);

    public async Task<List<PendingAction>> ListBySpokeAsync(Guid spokeId, PendingActionStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.PendingActions.Where(pa => pa.SpokeId == spokeId);
        if (status.HasValue)
            query = query.Where(pa => pa.Status == status.Value);
        return await query
            .Include(pa => pa.Spoke)
            .Include(pa => pa.Project)
            .Include(pa => pa.Job)
            .OrderByDescending(pa => pa.Priority)
            .ThenByDescending(pa => pa.CreatedAt)
            .ThenBy(pa => pa.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PendingAction>> ListAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.PendingActions.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(pa => pa.SpokeId == spokeId.Value);
        if (status.HasValue)
            query = query.Where(pa => pa.Status == status.Value);
        if (type.HasValue)
            query = query.Where(pa => pa.Type == type.Value);
        if (minPriority.HasValue)
            query = query.Where(pa => pa.Priority >= minPriority.Value);
        return await query
            .Include(pa => pa.Spoke)
            .Include(pa => pa.Project)
            .Include(pa => pa.Job)
            .OrderByDescending(pa => pa.Priority)
            .ThenByDescending(pa => pa.CreatedAt)
            .ThenBy(pa => pa.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<PendingAction> AddAsync(PendingAction entity, CancellationToken cancellationToken = default)
    {
        _context.PendingActions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PendingAction entity, CancellationToken cancellationToken = default)
    {
        _context.PendingActions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PendingActions.AsQueryable();
        if (spokeId.HasValue)
            query = query.Where(pa => pa.SpokeId == spokeId.Value);
        if (status.HasValue)
            query = query.Where(pa => pa.Status == status.Value);
        if (type.HasValue)
            query = query.Where(pa => pa.Type == type.Value);
        if (minPriority.HasValue)
            query = query.Where(pa => pa.Priority >= minPriority.Value);
        return await query.CountAsync(cancellationToken);
    }
}
