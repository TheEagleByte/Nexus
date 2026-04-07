using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Models;

public class PendingActionResponse
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public string SpokeName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string? ExternalKey { get; set; }
    public PendingActionType GateType { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Age { get; set; } = string.Empty;
    public JsonDocument? Metadata { get; set; }
}

public class PendingActionListResponse
{
    public List<PendingActionResponse> PendingActions { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

public class ResolvePendingActionRequest
{
    public required string Action { get; set; }
    public string? Notes { get; set; }
    public JsonDocument? Modifications { get; set; }
}

public class ResolvePendingActionResponse
{
    public Guid Id { get; set; }
    public PendingActionStatus Status { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset ResolvedAt { get; set; }
    public JsonDocument? Metadata { get; set; }
}
