using Nexus.Spoke.Models;

namespace Nexus.Spoke.Handlers;

public interface ICommandHandler
{
    string CommandType { get; }
    Task HandleAsync(CommandEnvelope command, CancellationToken cancellationToken);
}
