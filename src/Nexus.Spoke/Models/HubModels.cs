namespace Nexus.Spoke.Models;

// Registration (spoke sends to hub)
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

// Heartbeat (spoke sends to hub)
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

// Registration response (hub sends to spoke)
public record SpokeInfo(
    Guid SpokeId,
    string Name,
    SpokeStatus Status,
    DateTimeOffset RegisteredAt
);

public record ReconnectionPolicy(
    int InitialRetryDelayMs = 1000,
    int MaxRetryDelayMs = 300000,
    double BackoffMultiplier = 2.0
);

// Job assignment (hub sends to spoke)
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

// Job status (spoke sends to hub)
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

// Messages
public record SpokeMessage(
    string Content,
    Guid? JobId
);

// Job cancellation (hub sends to spoke)
public record JobCancellation(
    Guid JobId,
    string? Reason
);

// Conversation messages (hub ↔ spoke)
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

// Worker launch request (internal to spoke)
public record WorkerLaunchRequest(
    Guid JobId,
    string ProjectKey,
    JobType JobType,
    string PromptFilePath,
    string RepoPath,
    string OutputPath,
    string? SpokeSkillsPath,
    string? ProjectSkillsPath,
    string? MergedSkillsFilePath
);
