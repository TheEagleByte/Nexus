using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Hubs;

public class SpokeTimeoutService(
    IServiceScopeFactory scopeFactory,
    ILogger<SpokeTimeoutService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TimeoutThreshold = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SpokeTimeoutService started. Check interval: {Interval}s, timeout: {Timeout}s",
            CheckInterval.TotalSeconds, TimeoutThreshold.TotalSeconds);

        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckForTimedOutSpokesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error checking for timed-out spokes");
            }
        }
    }

    internal async Task CheckForTimedOutSpokesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var spokeService = scope.ServiceProvider.GetRequiredService<ISpokeService>();

        var cutoff = DateTimeOffset.UtcNow - TimeoutThreshold;

        var allSpokes = new List<Spoke>();
        await FetchAllSpokesWithStatusAsync(spokeService, SpokeStatus.Online, allSpokes, cancellationToken);
        await FetchAllSpokesWithStatusAsync(spokeService, SpokeStatus.Busy, allSpokes, cancellationToken);

        var staleSpokes = allSpokes
            .Where(s => s.LastSeen < cutoff)
            .ToList();

        foreach (var spoke in staleSpokes)
        {
            try
            {
                await spokeService.UpdateSpokeStatusAsync(spoke.Id, SpokeStatus.Offline, cancellationToken);
                logger.LogWarning(
                    "Spoke {SpokeId} ({SpokeName}) marked Offline — last seen {LastSeen}, threshold {Cutoff}",
                    spoke.Id, spoke.Name, spoke.LastSeen, cutoff);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark spoke {SpokeId} as offline", spoke.Id);
            }
        }
    }

    private static async Task FetchAllSpokesWithStatusAsync(
        ISpokeService spokeService, SpokeStatus status, List<Spoke> results, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var offset = 0;
        List<Spoke> batch;
        do
        {
            batch = await spokeService.ListSpokesAsync(status, limit: pageSize, offset: offset, cancellationToken: cancellationToken);
            results.AddRange(batch);
            offset += pageSize;
        } while (batch.Count == pageSize);
    }
}
