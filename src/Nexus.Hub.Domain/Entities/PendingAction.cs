using System.Text.Json;

namespace Nexus.Hub.Domain.Entities;

public class PendingAction
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid JobId { get; set; }
    public PendingActionType Type { get; set; }
    public PendingActionStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public JsonDocument? Metadata { get; set; }

    public Spoke Spoke { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
