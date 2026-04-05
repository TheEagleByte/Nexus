using Nexus.Spoke.Handlers;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class CommandQueueWorker(
    CommandQueue queue,
    CommandHandlerRegistry registry,
    ILogger<CommandQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CommandQueueWorker started. Registered handlers: [{Types}]",
            string.Join(", ", registry.RegisteredTypes));

        await foreach (var command in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                var handler = registry.GetHandler(command.CommandType);
                if (handler is null)
                {
                    logger.LogWarning("No handler registered for command type {CommandType}", command.CommandType);
                    continue;
                }

                logger.LogDebug("Processing command {CommandType} (received: {ReceivedAt})",
                    command.CommandType, command.ReceivedAt);

                await handler.HandleAsync(command, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error processing command {CommandType}", command.CommandType);
            }
        }
    }
}
