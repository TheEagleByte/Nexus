using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Workers;

public class JobTimeoutMonitor(
    ActiveJobTracker activeJobTracker,
    IDockerService dockerService,
    IJobLifecycleService lifecycleService,
    IOptions<SpokeConfiguration> config,
    ILogger<JobTimeoutMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Job timeout monitor started (timeout: {Timeout}s, check interval: {Interval}s)",
            config.Value.Docker.TimeoutSeconds, CheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await CheckForTimedOutJobsAsync();
        }
    }

    internal async Task CheckForTimedOutJobsAsync()
    {
        var timeout = TimeSpan.FromSeconds(config.Value.Docker.TimeoutSeconds);
        var activeJobs = activeJobTracker.GetAll();

        foreach (var job in activeJobs)
        {
            var elapsed = DateTimeOffset.UtcNow - job.StartedAt;
            if (elapsed <= timeout)
                continue;

            logger.LogWarning("Job {JobId} timed out after {Elapsed}", job.JobId, elapsed);

            try
            {
                // Cancel the per-job token
                try
                {
                    await job.Cts.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed
                }

                // Kill the container
                await dockerService.KillContainerAsync(job.ContainerId, CancellationToken.None);

                // Report as failed (timeout is a failure, not a cancellation)
                var hours = elapsed.TotalHours;
                await lifecycleService.ReportStatusAsync(
                    job.JobId,
                    job.ProjectId,
                    job.ProjectKey,
                    JobStatus.Running,
                    JobStatus.Failed,
                    $"Job timed out after {hours:F1} hours (limit: {timeout.TotalHours:F1}h)",
                    cancellationToken: CancellationToken.None);

                // Cleanup
                if (activeJobTracker.TryRemove(job.JobId, out var removedJob))
                    removedJob?.Cts.Dispose();
                await dockerService.RemoveContainerAsync(job.ContainerId, CancellationToken.None);

                logger.LogInformation("Timed-out job {JobId} killed and cleaned up", job.JobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling timeout for job {JobId}", job.JobId);
            }
        }
    }
}
