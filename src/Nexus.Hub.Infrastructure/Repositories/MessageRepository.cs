using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class MessageRepository(NexusDbContext context) : IMessageRepository
{
    private readonly NexusDbContext _context = context;

    public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Message>> ListBySpokeAsync(Guid spokeId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CountBySpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
