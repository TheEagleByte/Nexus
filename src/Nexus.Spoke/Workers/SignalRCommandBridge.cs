using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

/// <summary>
/// Bridges SignalR hub events into the command queue for sequential processing.
/// Registered as a hosted service so it runs before the connection worker connects.
/// </summary>
public class SignalRCommandBridge(
    IHubConnectionService connectionService,
    CommandQueue commandQueue,
    ILogger<SignalRCommandBridge> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        connectionService.OnReceived<JobAssignment>("AssignJob", async assignment =>
        {
            logger.LogInformation("Received job assignment {JobId} from hub", assignment.JobId);
            await commandQueue.EnqueueAsync(
                new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow));
        });

        connectionService.OnReceived<object>("ReceiveMessage", async message =>
        {
            logger.LogInformation("Received message from hub");
            await commandQueue.EnqueueAsync(
                new CommandEnvelope("message.to_spoke", message, DateTimeOffset.UtcNow));
        });

        connectionService.OnReceived<object>("JobCancelled", async cancelMsg =>
        {
            logger.LogInformation("Received job cancellation from hub");
            await commandQueue.EnqueueAsync(
                new CommandEnvelope("job.cancel", cancelMsg, DateTimeOffset.UtcNow));
        });

        connectionService.OnReceived<ConversationUserMessage>("SendConversationMessage", async message =>
        {
            logger.LogInformation("Received conversation message for {ConversationId}", message.ConversationId);
            await commandQueue.EnqueueAsync(
                new CommandEnvelope("conversation.message", message, DateTimeOffset.UtcNow));
        });

        logger.LogDebug("SignalR command bridge registered");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
