namespace Nexus.Spoke.Services;

public interface IWorkerOutputStreamer
{
    Task StreamAsync(
        Guid jobId,
        Guid projectId,
        string projectKey,
        string containerId,
        CancellationToken cancellationToken = default);
}
