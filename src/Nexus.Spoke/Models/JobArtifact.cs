namespace Nexus.Spoke.Models;

public record JobArtifact(
    Guid JobId,
    string ProjectKey,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Summary
);
