namespace Nexus.Hub.Domain.Entities;

public class OutputStream
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public long Sequence { get; set; }
    public string Content { get; set; } = string.Empty;
    public string StreamType { get; set; } = "stdout";
    public DateTimeOffset Timestamp { get; set; }

    public Job Job { get; set; } = null!;
}
