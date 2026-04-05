using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class MessageRepository(NexusDbContext context) : IMessageRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Messages.FindAsync([id], cancellationToken);

    public async Task<List<Message>> ListBySpokeAsync(Guid spokeId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => await _context.Messages
            .Where(m => m.SpokeId == spokeId)
            .OrderBy(m => m.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<int> CountBySpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => await _context.Messages.CountAsync(m => m.SpokeId == spokeId, cancellationToken);
}
