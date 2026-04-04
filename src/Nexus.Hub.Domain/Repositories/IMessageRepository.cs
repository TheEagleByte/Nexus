using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id);
    Task<List<Message>> ListBySpokeAsync(Guid spokeId, int limit = 50, int offset = 0);
    Task<Message> AddAsync(Message message);
    Task<int> CountBySpokeAsync(Guid spokeId);
}
