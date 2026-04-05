namespace Nexus.Spoke.Models;

public record StatusMetadata(
    ProjectStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<StatusHistoryEntry> History
);

public record StatusHistoryEntry(
    ProjectStatus From,
    ProjectStatus To,
    DateTimeOffset Timestamp
);

public record TicketMetadata(
    string Key,
    string Summary,
    string? Description,
    string[]? AcceptanceCriteria,
    string? IssueType,
    string[]? Labels,
    string? Assignee
);
