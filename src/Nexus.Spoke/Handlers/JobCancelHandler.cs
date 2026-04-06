using System.Text.Json;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Handlers;

public class JobCancelHandler(
    IDockerService dockerService,
    IJobLifecycleService lifecycleService,
    ActiveJobTracker activeJobTracker,
    ILogger<JobCancelHandler> logger) : ICommandHandler
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string CommandType => "job.cancel";

    public async Task HandleAsync(CommandEnvelope command, CancellationToken cancellationToken)
    {
        var cancellation = DeserializePayload(command.Payload);
        if (cancellation is null)
        {
            logger.LogError("Failed to deserialize JobCancellation from command payload");
            return;
        }

        logger.LogInformation("Cancelling job {JobId}, reason: {Reason}",
            cancellation.JobId, cancellation.Reason ?? "none");

        var activeJob = activeJobTracker.Get(cancellation.JobId);
        if (activeJob is null)
        {
            logger.LogWarning("Job {JobId} not found in active tracker — may have already completed",
                cancellation.JobId);
            return;
        }

        // Cancel the per-job token — this cascades to StreamAsync and WaitForExitAsync
        try
        {
            await activeJob.Cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed, job is finishing up
        }

        // Kill the container
        await dockerService.KillContainerAsync(activeJob.ContainerId, CancellationToken.None);

        // Report cancellation
        await lifecycleService.ReportStatusAsync(
            activeJob.JobId,
            activeJob.ProjectId,
            activeJob.ProjectKey,
            JobStatus.Running,
            JobStatus.Cancelled,
            cancellation.Reason ?? "Cancelled by hub",
            cancellationToken: CancellationToken.None);

        // Remove from tracker, dispose CTS, and cleanup container
        if (activeJobTracker.TryRemove(cancellation.JobId, out var removedJob))
            removedJob?.Cts.Dispose();
        await dockerService.RemoveContainerAsync(activeJob.ContainerId, CancellationToken.None);

        logger.LogInformation("Job {JobId} cancelled and cleaned up", cancellation.JobId);
    }

    private static JobCancellation? DeserializePayload(object payload)
    {
        if (payload is JobCancellation cancellation)
            return cancellation;

        if (payload is JsonElement element)
            return JsonSerializer.Deserialize<JobCancellation>(element.GetRawText(), DeserializeOptions);

        var json = JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<JobCancellation>(json, DeserializeOptions);
    }
}
