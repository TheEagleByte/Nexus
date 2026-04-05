namespace Nexus.Spoke.Models;

public record CommandEnvelope(
    string CommandType,
    object Payload,
    DateTimeOffset ReceivedAt
);
