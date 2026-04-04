namespace Nexus.Hub.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public MessageDirection Direction { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? JobId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public Spoke Spoke { get; set; } = null!;
    public Job? Job { get; set; }
}
