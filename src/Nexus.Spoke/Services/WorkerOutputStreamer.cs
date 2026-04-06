using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class WorkerOutputStreamer(
    IDockerService dockerService,
    IHubConnectionService hubConnection,
    IJobArtifactService jobArtifacts,
    ILogger<WorkerOutputStreamer> logger) : IWorkerOutputStreamer
{
    public async Task StreamAsync(
        Guid jobId,
        Guid projectId,
        string projectKey,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var spokeId = hubConnection.SpokeId ?? Guid.Empty;
        long sequence = 0;

        logger.LogInformation("Starting output stream for job {JobId} container {ContainerId}",
            jobId, containerId[..Math.Min(12, containerId.Length)]);

        try
        {
            await foreach (var (content, streamType) in dockerService.StreamOutputAsync(containerId, cancellationToken))
            {
                sequence++;

                // Persist locally
                await jobArtifacts.AppendOutputAsync(projectKey, jobId, content);

                // Forward to hub
                var chunk = new JobOutputChunk(
                    jobId,
                    spokeId,
                    sequence,
                    content,
                    streamType,
                    DateTimeOffset.UtcNow);

                try
                {
                    await hubConnection.SendAsync("StreamJobOutput", chunk, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Don't fail the whole stream if a single chunk fails to send
                    logger.LogWarning(ex, "Failed to send output chunk {Sequence} for job {JobId}", sequence, jobId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Output streaming cancelled for job {JobId}", jobId);
        }

        logger.LogInformation("Output streaming completed for job {JobId} — {ChunkCount} chunks", jobId, sequence);
    }
}
