using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class HeartbeatWorker(
    IHubConnectionService connectionService,
    IOptions<SpokeConfiguration> config,
    ResourceMonitor resourceMonitor,
    ILogger<HeartbeatWorker> logger,
    IRepoPoolService? repoPool = null,
    ICodebaseMemoryMcpService? mcpService = null) : BackgroundService
{
    internal static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
    internal const int MaxMissedBeforeError = 3;

    private volatile int _consecutiveMissed;
    private volatile bool _ackReceived;
    private bool _ackHandlerRegistered;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = config.Value.Approval.HeartbeatIntervalSeconds;
        var interval = intervalSeconds > 0
            ? TimeSpan.FromSeconds(intervalSeconds)
            : DefaultHeartbeatInterval;

        logger.LogInformation("HeartbeatWorker started. Interval: {Interval}s", interval.TotalSeconds);

        RegisterAckHandler();

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!connectionService.IsConnected)
            {
                logger.LogDebug("Skipping heartbeat: not connected to hub");
                continue;
            }

            try
            {
                await SendHeartbeatAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error sending heartbeat");
            }
        }
    }

    private void RegisterAckHandler()
    {
        if (_ackHandlerRegistered) return;
        _ackHandlerRegistered = true;

        connectionService.OnReceived<Guid, DateTimeOffset>("HeartbeatAcknowledged", (_, _) =>
        {
            _ackReceived = true;
            _consecutiveMissed = 0;
            return Task.CompletedTask;
        });
    }

    internal async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        _ackReceived = false;

        var spokeId = connectionService.SpokeId ?? Guid.Empty;

        Dictionary<string, string>? metadata = null;
        if (repoPool is not null)
        {
            var states = repoPool.GetSyncStates();
            if (states.Count > 0)
            {
                metadata = states.ToDictionary(
                    kvp => $"repo:{kvp.Key}",
                    kvp => $"{kvp.Value.Status}|{kvp.Value.LastSyncedAt?.ToString("O") ?? "never"}");
            }
        }

        if (mcpService is not null)
        {
            metadata ??= new Dictionary<string, string>();
            var mcpStatus = mcpService.GetStatus();
            var mcpEndpoint = mcpService.GetEndpoint();
            metadata["mcp:codebase-memory"] = mcpEndpoint is not null
                ? $"{mcpStatus}|{mcpEndpoint}"
                : mcpStatus.ToString();
        }

        var heartbeat = new SpokeHeartbeat(
            SpokeId: spokeId,
            Status: SpokeStatus.Online,  // Future: track actual busy/idle state
            ActiveJobCount: 0,            // Future: get from job tracking service
            ResourceUsage: resourceMonitor.GetCurrentUsage(),
            Timestamp: DateTimeOffset.UtcNow,
            Metadata: metadata
        );

        await connectionService.SendAsync("Heartbeat", heartbeat, cancellationToken);

        // Wait for ack
        await Task.Delay(AckTimeout, cancellationToken);

        if (!_ackReceived)
        {
            _consecutiveMissed++;
            logger.LogWarning(
                "Heartbeat acknowledgement not received within {Timeout}s (consecutive missed: {Count})",
                AckTimeout.TotalSeconds, _consecutiveMissed);

            if (_consecutiveMissed >= MaxMissedBeforeError)
            {
                logger.LogError(
                    "{Count} consecutive heartbeats missed. Hub may be unreachable. Spoke continues operating.",
                    _consecutiveMissed);
            }
        }
    }
}
