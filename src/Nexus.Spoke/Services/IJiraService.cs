using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IJiraService
{
    Task<TicketMetadata?> FetchTicketAsync(string ticketKey, CancellationToken cancellationToken = default);
}
