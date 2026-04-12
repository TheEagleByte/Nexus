using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class RepoPoolSyncWorker(
    IRepoPoolService repoPool,
    IOptions<SpokeConfiguration> config,
    ILogger<RepoPoolSyncWorker> logger,
    ICodebaseMemoryMcpService? mcpService = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Value.Capabilities.Git)
        {
            logger.LogInformation("Git capability disabled, RepoPoolSyncWorker exiting");
            return;
        }

        logger.LogInformation("RepoPoolSyncWorker starting, performing initial clone");

        try
        {
            await repoPool.InitializeAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Repo pool initialization failed, continuing to sync loop");
        }

        if (mcpService is not null)
        {
            try
            {
                await mcpService.ReindexAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to trigger MCP reindex after initial clone");
            }
        }

        var intervalSeconds = Math.Max(30, config.Value.GitProvider.SyncIntervalSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        logger.LogInformation("Repo pool sync interval: {Interval}s", intervalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await repoPool.SyncAllAsync(stoppingToken);

                // Notify MCP server to reindex after sync completes
                if (mcpService is not null)
                {
                    try
                    {
                        await mcpService.ReindexAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "MCP reindex after sync failed, classification still works via raw repo access");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during repo pool sync cycle");
            }
        }
    }
}
