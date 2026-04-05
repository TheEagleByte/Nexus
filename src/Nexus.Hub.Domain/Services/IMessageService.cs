using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IMessageService
{
    Task<Message> RecordMessageAsync(Guid spokeId, MessageDirection direction, string content, Guid? jobId = null, CancellationToken cancellationToken = default);
    Task<List<Message>> GetConversationAsync(Guid spokeId, Guid? jobId = null, MessageDirection? direction = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> GetMessageCountAsync(Guid spokeId, Guid? jobId = null, MessageDirection? direction = null, CancellationToken cancellationToken = default);
}
