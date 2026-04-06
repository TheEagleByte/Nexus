using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class ConversationService(
    IConversationRepository conversationRepository,
    ISpokeRepository spokeRepository,
    ILogger<ConversationService> logger) : IConversationService
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly ISpokeRepository _spokeRepository = spokeRepository;
    private readonly ILogger<ConversationService> _logger = logger;

    public async Task<Conversation> CreateConversationAsync(Guid? spokeId, string title, CancellationToken cancellationToken = default)
    {
        if (spokeId.HasValue)
        {
            var spoke = await _spokeRepository.GetByIdAsync(spokeId.Value, cancellationToken);
            if (spoke is null)
                throw new NotFoundException($"Spoke with id '{spokeId}' not found");
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now,
            IsArchived = false
        };

        await _conversationRepository.AddAsync(conversation, cancellationToken);

        // Re-fetch with spoke navigation property loaded
        if (spokeId.HasValue)
        {
            conversation = await _conversationRepository.GetByIdAsync(conversation.Id, cancellationToken);
        }

        _logger.LogInformation("Conversation created: {ConversationId} for spoke {SpokeId}", conversation!.Id, spokeId);
        return conversation;
    }

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
        => _conversationRepository.GetByIdAsync(id, cancellationToken);

    public Task<Conversation?> GetConversationWithMessagesAsync(Guid id, int messageLimit = 50, int messageOffset = 0, CancellationToken cancellationToken = default)
        => _conversationRepository.GetByIdWithMessagesAsync(id, messageLimit, messageOffset, cancellationToken);

    public Task<List<Conversation>> ListConversationsAsync(Guid? spokeId = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _conversationRepository.ListAsync(spokeId, limit, offset, cancellationToken);

    public Task<int> GetConversationCountAsync(Guid? spokeId = null, CancellationToken cancellationToken = default)
        => _conversationRepository.CountAsync(spokeId, cancellationToken);

    public async Task ArchiveConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Conversation with id '{id}' not found");

        conversation.IsArchived = true;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        _logger.LogInformation("Conversation archived: {ConversationId}", id);
    }

    public async Task<ConversationMessage> AddMessageAsync(Guid conversationId, ConversationRole role, string content, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation with id '{conversationId}' not found");

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _conversationRepository.AddMessageAsync(message, cancellationToken);

        conversation.UpdatedAt = message.Timestamp;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        _logger.LogInformation("Message added to conversation {ConversationId}: {MessageId} ({Role})",
            conversationId, message.Id, role);
        return message;
    }

    public Task<int> GetMessageCountAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => _conversationRepository.CountMessagesAsync(conversationId, cancellationToken);

    public async Task SetCcSessionIdAsync(Guid conversationId, string ccSessionId, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation with id '{conversationId}' not found");

        conversation.CcSessionId = ccSessionId;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    }
}
