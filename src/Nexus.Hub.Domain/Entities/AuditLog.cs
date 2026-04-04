using System.Text.Json;

namespace Nexus.Hub.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public Guid? SpokeId { get; set; }
    public string Action { get; set; } = string.Empty;
    public JsonDocument? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public User? User { get; set; }
    public Spoke? Spoke { get; set; }
}
