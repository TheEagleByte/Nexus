using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Models;

public class SendMessageRequest
{
    public required string Content { get; set; }
    public Guid? JobId { get; set; }
}

public class MessageResponse
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public MessageDirection Direction { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? JobId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class CreateMessageRequest
{
    public Guid SpokeId { get; set; }
    public MessageDirection Direction { get; set; }
    public required string Content { get; set; }
    public Guid? JobId { get; set; }
}

public class ConversationResponse
{
    public List<MessageResponse> Messages { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
