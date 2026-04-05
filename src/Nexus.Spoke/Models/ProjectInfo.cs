namespace Nexus.Spoke.Models;

public record ProjectInfo(
    string ProjectKey,
    string Name,
    ProjectStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Summary,
    string? ExternalKey
);
