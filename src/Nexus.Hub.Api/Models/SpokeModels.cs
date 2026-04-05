using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Models;

public class SpokeRegistrationRequest
{
    public required string Psk { get; set; }
    public required string Name { get; set; }
    public string[] Capabilities { get; set; } = [];
    public string? Os { get; set; }
    public string? Architecture { get; set; }
    public JsonDocument? Config { get; set; }
    public JsonDocument? Profile { get; set; }
    public JsonDocument? Metadata { get; set; }
}

public class UpdateSpokeConfigRequest
{
    public string? Name { get; set; }
    public string? ApprovalMode { get; set; }
    public int? MaxConcurrentJobs { get; set; }
}

public class SpokeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpokeStatus Status { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public int ActiveJobCount { get; set; }
    public JsonDocument? Capabilities { get; set; }
    public JsonDocument? Config { get; set; }
}

public class SpokeDetailResponse : SpokeResponse
{
    public JsonDocument? Profile { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public int TotalJobsCompleted { get; set; }
    public ResourceUsage? ResourceUsage { get; set; }
}

public class ResourceUsage
{
    public double? MemoryUsageMb { get; set; }
    public double? CpuUsagePercent { get; set; }
    public double? DiskUsageMb { get; set; }
}

public class SpokeListResponse
{
    public List<SpokeResponse> Spokes { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
