using System.Text.Json;

namespace Nexus.Hub.Domain.Entities;

public class Spoke
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpokeStatus Status { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public JsonDocument Capabilities { get; set; } = null!;
    public JsonDocument Config { get; set; } = null!;
    public JsonDocument? Profile { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
