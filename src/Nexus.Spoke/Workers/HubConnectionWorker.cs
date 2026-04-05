using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class HubConnectionWorker(
    IHubConnectionService connectionService,
    ILogger<HubConnectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HubConnectionWorker starting");

        try
        {
            await connectionService.ConnectAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to connect to hub on startup. Will rely on auto-reconnect.");
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        await connectionService.DisconnectAsync(CancellationToken.None);
        logger.LogInformation("HubConnectionWorker stopped");
    }
}
