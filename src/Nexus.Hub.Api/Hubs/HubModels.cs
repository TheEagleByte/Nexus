using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Hubs;

public record SpokeRegistration(
    string Name,
    string[] Capabilities,
    string Os,
    string Architecture,
    SpokeConfigDto Config,
    SpokeProfileDto? Profile,
    Dictionary<string, string>? Metadata
);

public record SpokeConfigDto(
    string ApprovalMode,
    int MaxConcurrentJobs,
    int HeartbeatIntervalSeconds
);

public record SpokeProfileDto(
    string DisplayName,
    string MachineDescription,
    RepositoryDto[] Repos,
    JiraConfigDto? JiraConfig,
    string[] Integrations,
    string Description
);

public record RepositoryDto(
    string Name,
    string RemoteUrl
);

public record JiraConfigDto(
    string InstanceUrl,
    string[] ProjectKeys
);

public record SpokeHeartbeat(
    Guid SpokeId,
    SpokeStatus Status,
    int ActiveJobCount,
    ResourceUsageDto ResourceUsage,
    DateTimeOffset Timestamp
);

public record ResourceUsageDto(
    long MemoryUsageMb,
    double CpuUsagePercent,
    long DiskUsageMb
);

public record SpokeInfo(
    Guid SpokeId,
    string Name,
    SpokeStatus Status,
    DateTimeOffset RegisteredAt
);


public record JobAssignment(
    Guid JobId,
    Guid ProjectId,
    JobType Type,
    string Context,
    JobParameters Parameters,
    bool RequireApproval,
    DateTimeOffset AssignedAt
);

public record JobParameters(
    Dictionary<string, object>? CustomFields
);

public record JobStatusChangedEvent(
    Guid JobId,
    Guid ProjectId,
    Guid SpokeId,
    JobStatus NewStatus,
    JobStatus PreviousStatus,
    string? Summary,
    Dictionary<string, object>? Metadata,
    DateTimeOffset Timestamp
);

public record JobOutputChunk(
    Guid JobId,
    Guid SpokeId,
    long Sequence,
    string Content,
    string StreamType,
    DateTimeOffset Timestamp
);

public record SpokeMessage(
    string Content,
    Guid? JobId
);

public record ReconnectionPolicy(
    int InitialRetryDelayMs = 1000,
    int MaxRetryDelayMs = 300000,
    double BackoffMultiplier = 2.0
);

public record ConversationMessageReceivedEvent(
    Guid ConversationId,
    Guid MessageId,
    string Role,
    string Content,
    DateTimeOffset Timestamp,
    bool Streaming
);

public record ConversationUserMessage(
    Guid ConversationId,
    Guid SpokeId,
    string Content,
    DateTimeOffset Timestamp
);

public record ConversationSpokeMessage(
    Guid ConversationId,
    string Content,
    DateTimeOffset Timestamp
);
