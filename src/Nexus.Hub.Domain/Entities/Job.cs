namespace Nexus.Hub.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SpokeId { get; set; }
    public JobStatus Status { get; set; }
    public JobType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Summary { get; set; }
    public bool ApprovalRequired { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    public Project Project { get; set; } = null!;
    public Spoke Spoke { get; set; } = null!;
    public ICollection<OutputStream> OutputStreams { get; set; } = new List<OutputStream>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
