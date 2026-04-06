using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class ConversationRepository(NexusDbContext context) : IConversationRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Conversations
            .Include(c => c.Spoke)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsArchived, cancellationToken);

    public async Task<Conversation?> GetByIdWithMessagesAsync(Guid id, int messageLimit = 50, int messageOffset = 0, CancellationToken cancellationToken = default)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Spoke)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsArchived, cancellationToken);

        if (conversation is null)
            return null;

        conversation.Messages = await _context.ConversationMessages
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.Timestamp)
            .ThenBy(m => m.Id)
            .Skip(messageOffset)
            .Take(messageLimit)
            .ToListAsync(cancellationToken);

        return conversation;
    }

    public async Task<List<Conversation>> ListAsync(Guid? spokeId = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var query = _context.Conversations
            .Include(c => c.Spoke)
            .Where(c => !c.IsArchived);

        if (spokeId.HasValue)
            query = query.Where(c => c.SpokeId == spokeId.Value);

        return await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid? spokeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Conversations.Where(c => !c.IsArchived);
        if (spokeId.HasValue)
            query = query.Where(c => c.SpokeId == spokeId.Value);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _context.Conversations.Update(conversation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationMessage> AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        _context.ConversationMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<int> CountMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
        => await _context.ConversationMessages.CountAsync(m => m.ConversationId == conversationId, cancellationToken);
}
