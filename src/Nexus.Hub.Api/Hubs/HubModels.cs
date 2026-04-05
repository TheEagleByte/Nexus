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
    DateTime Timestamp
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
