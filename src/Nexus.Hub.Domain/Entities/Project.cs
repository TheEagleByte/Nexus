namespace Nexus.Hub.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public string? ExternalKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Spoke Spoke { get; set; } = null!;
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
