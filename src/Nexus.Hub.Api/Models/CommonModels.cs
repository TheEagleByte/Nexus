using System.Text.Json;

namespace Nexus.Hub.Api.Models;

public class ErrorResponse
{
    public required ErrorDetail Error { get; set; }
}

public class ErrorDetail
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public int Status { get; set; } = 500;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
    public JsonDocument? Details { get; set; }
}
