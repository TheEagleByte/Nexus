using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Message>> ListBySpokeAsync(Guid spokeId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default);
    Task<int> CountBySpokeAsync(Guid spokeId, CancellationToken cancellationToken = default);
}
