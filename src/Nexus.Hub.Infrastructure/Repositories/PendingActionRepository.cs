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

    public async Task<List<PendingAction>> ListAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, int limit = 50, int offset = 0, bool sortAscending = true, CancellationToken cancellationToken = default)
    {
        var query = _context.PendingActions
            .Include(pa => pa.Spoke)
            .Include(pa => pa.Project)
            .Include(pa => pa.Job)
            .AsQueryable();

        if (spokeId.HasValue)
            query = query.Where(pa => pa.SpokeId == spokeId.Value);
        if (projectId.HasValue)
            query = query.Where(pa => pa.ProjectId == projectId.Value);
        if (type.HasValue)
            query = query.Where(pa => pa.Type == type.Value);
        if (status.HasValue)
            query = query.Where(pa => pa.Status == status.Value);

        query = sortAscending
            ? query.OrderBy(pa => pa.CreatedAt).ThenBy(pa => pa.Id)
            : query.OrderByDescending(pa => pa.CreatedAt).ThenBy(pa => pa.Id);

        return await query.Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<PendingAction> AddAsync(PendingAction action, CancellationToken cancellationToken = default)
    {
        _context.PendingActions.Add(action);
        await _context.SaveChangesAsync(cancellationToken);
        return action;
    }

    public async Task UpdateAsync(PendingAction action, CancellationToken cancellationToken = default)
    {
        _context.PendingActions.Update(action);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PendingActions.AsQueryable();

        if (spokeId.HasValue)
            query = query.Where(pa => pa.SpokeId == spokeId.Value);
        if (projectId.HasValue)
            query = query.Where(pa => pa.ProjectId == projectId.Value);
        if (type.HasValue)
            query = query.Where(pa => pa.Type == type.Value);
        if (status.HasValue)
            query = query.Where(pa => pa.Status == status.Value);

        return await query.CountAsync(cancellationToken);
    }
}
