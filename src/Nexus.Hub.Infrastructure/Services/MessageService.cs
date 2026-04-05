using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class MessageService(IMessageRepository messageRepository, ILogger<MessageService> logger) : IMessageService
{
    private readonly IMessageRepository _messageRepository = messageRepository;
    private readonly ILogger<MessageService> _logger = logger;

    public async Task<Message> RecordMessageAsync(Guid spokeId, MessageDirection direction, string content, Guid? jobId = null, CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Direction = direction,
            Content = content,
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _messageRepository.AddAsync(message, cancellationToken);
        _logger.LogInformation("Message recorded: {MessageId} ({Direction}) for spoke {SpokeId}", message.Id, direction, spokeId);
        return message;
    }

    public Task<List<Message>> GetConversationAsync(Guid spokeId, Guid? jobId = null, MessageDirection? direction = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _messageRepository.ListBySpokeAsync(spokeId, jobId, direction, limit, offset, cancellationToken);

    public Task<int> GetMessageCountAsync(Guid spokeId, Guid? jobId = null, MessageDirection? direction = null, CancellationToken cancellationToken = default)
        => _messageRepository.CountBySpokeAsync(spokeId, jobId, direction, cancellationToken);
}
