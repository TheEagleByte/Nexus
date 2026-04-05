using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class SpokeRepository(NexusDbContext context) : ISpokeRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Spoke?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Spokes.FindAsync([id], cancellationToken);

    public async Task<List<Spoke>> ListAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Spokes.AsQueryable();
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        return await query.OrderByDescending(s => s.CreatedAt).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<Spoke> AddAsync(Spoke spoke, CancellationToken cancellationToken = default)
    {
        _context.Spokes.Add(spoke);
        await _context.SaveChangesAsync(cancellationToken);
        return spoke;
    }

    public async Task UpdateAsync(Spoke spoke, CancellationToken cancellationToken = default)
    {
        _context.Spokes.Update(spoke);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var spoke = await _context.Spokes.FindAsync([id], cancellationToken);
        if (spoke is not null)
        {
            _context.Spokes.Remove(spoke);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Spokes.AsQueryable();
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        return await query.CountAsync(cancellationToken);
    }
}
