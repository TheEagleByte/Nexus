using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class JobLifecycleService(
    IHubConnectionService hubConnection,
    IJobArtifactService jobArtifacts,
    ILogger<JobLifecycleService> logger) : IJobLifecycleService
{
    public async Task ReportStatusAsync(
        Guid jobId,
        Guid projectId,
        string projectKey,
        JobStatus previousStatus,
        JobStatus newStatus,
        string? summary = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Job {JobId} status: {Previous} → {New}", jobId, previousStatus, newStatus);

        // Write local status artifact
        await jobArtifacts.WriteStatusAsync(projectKey, jobId, newStatus, metadata);

        // Report to hub via SignalR
        var spokeId = hubConnection.SpokeId ?? Guid.Empty;
        var evt = new JobStatusChangedEvent(
            jobId,
            projectId,
            spokeId,
            newStatus,
            previousStatus,
            summary,
            metadata,
            DateTimeOffset.UtcNow);

        try
        {
            await hubConnection.SendAsync("ReportJobStatusChanged", evt, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to report job {JobId} status change to hub", jobId);
        }
    }
}
