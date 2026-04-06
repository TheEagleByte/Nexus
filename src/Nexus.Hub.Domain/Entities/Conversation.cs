namespace Nexus.Hub.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid? SpokeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CcSessionId { get; set; }
    public bool IsArchived { get; set; }

    public Spoke? Spoke { get; set; }
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
