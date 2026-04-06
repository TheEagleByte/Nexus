namespace Nexus.Hub.Domain.Entities;

public class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }

    public Conversation Conversation { get; set; } = null!;
}
