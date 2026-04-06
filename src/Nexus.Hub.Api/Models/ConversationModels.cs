namespace Nexus.Hub.Api.Models;

public class CreateConversationRequest
{
    public Guid? SpokeId { get; set; }
    public required string Title { get; set; }
}

public class SendConversationMessageRequest
{
    public required string Content { get; set; }
}

public class ConversationSummaryResponse
{
    public Guid Id { get; set; }
    public Guid? SpokeId { get; set; }
    public string? SpokeName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CcSessionId { get; set; }
    public int MessageCount { get; set; }
}

public class ConversationDetailResponse : ConversationSummaryResponse
{
    public List<ConversationMessageResponse> Messages { get; set; } = [];
}

public class ConversationMessageResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public class ConversationListResponse
{
    public List<ConversationSummaryResponse> Conversations { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
