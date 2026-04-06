using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdWithMessagesAsync(Guid id, int messageLimit = 50, int messageOffset = 0, CancellationToken cancellationToken = default);
    Task<List<Conversation>> ListAsync(Guid? spokeId = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, CancellationToken cancellationToken = default);
    Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task<ConversationMessage> AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);
    Task<int> CountMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
