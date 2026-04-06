using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IDockerService : IAsyncDisposable
{
    Task EnsureImageAsync(CancellationToken cancellationToken = default);

    Task<string> LaunchWorkerAsync(WorkerLaunchRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<(string Content, string StreamType)> StreamOutputAsync(
        string containerId, CancellationToken cancellationToken = default);

    Task<long> WaitForExitAsync(string containerId, CancellationToken cancellationToken = default);

    Task KillContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default);
}
