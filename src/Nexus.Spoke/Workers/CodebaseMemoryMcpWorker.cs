using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class CodebaseMemoryMcpWorker(
    ICodebaseMemoryMcpService mcpService,
    IOptions<SpokeConfiguration> config,
    ILogger<CodebaseMemoryMcpWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Value.CodebaseMemoryMcp.Enabled)
        {
            logger.LogInformation("Codebase Memory MCP is disabled, worker exiting");
            return;
        }

        logger.LogInformation("CodebaseMemoryMcpWorker starting");

        try
        {
            await mcpService.StartAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "MCP server initial startup failed, continuing to health check loop");
        }

        var intervalSeconds = Math.Max(10, config.Value.CodebaseMemoryMcp.HealthCheckIntervalSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        logger.LogInformation("MCP health check interval: {Interval}s", intervalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!mcpService.IsHealthy())
                {
                    logger.LogWarning("MCP server health check failed, attempting restart");
                    await mcpService.StartAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during MCP server health check/restart");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("CodebaseMemoryMcpWorker stopping");

        try
        {
            await mcpService.StopAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error stopping MCP server during worker shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
