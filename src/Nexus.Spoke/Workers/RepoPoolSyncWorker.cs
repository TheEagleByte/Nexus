using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class RepoPoolSyncWorker(
    IRepoPoolService repoPool,
    IOptions<SpokeConfiguration> config,
    ILogger<RepoPoolSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RepoPoolSyncWorker starting, performing initial clone");
        await repoPool.InitializeAsync(stoppingToken);

        var intervalSeconds = Math.Max(30, config.Value.GitProvider.SyncIntervalSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        logger.LogInformation("Repo pool sync interval: {Interval}s", intervalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await repoPool.SyncAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during repo pool sync cycle");
            }
        }
    }
}
