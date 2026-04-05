using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class MessageService(IMessageRepository messageRepository, ILogger<MessageService> logger) : IMessageService
{
    private readonly IMessageRepository _messageRepository = messageRepository;
    private readonly ILogger<MessageService> _logger = logger;

    public Task<Message> RecordMessageAsync(Guid spokeId, MessageDirection direction, string content, Guid? jobId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Message>> GetConversationAsync(Guid spokeId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetMessageCountAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
