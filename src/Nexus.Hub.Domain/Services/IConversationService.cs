using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IConversationService
{
    Task<Conversation> CreateConversationAsync(Guid? spokeId, string title, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationWithMessagesAsync(Guid id, int messageLimit = 50, int messageOffset = 0, CancellationToken cancellationToken = default);
    Task<List<Conversation>> ListConversationsAsync(Guid? spokeId = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> GetConversationCountAsync(Guid? spokeId = null, CancellationToken cancellationToken = default);
    Task ArchiveConversationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ConversationMessage> AddMessageAsync(Guid conversationId, ConversationRole role, string content, CancellationToken cancellationToken = default);
    Task<int> GetMessageCountAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task SetCcSessionIdAsync(Guid conversationId, string ccSessionId, CancellationToken cancellationToken = default);
}
