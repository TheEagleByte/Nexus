namespace Nexus.Spoke.Services;

public interface ICodebaseMemoryMcpService
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    bool IsHealthy();
    Task ReindexAsync(CancellationToken ct);
    string? GetEndpoint();
    CodebaseMemoryMcpStatus GetStatus();
}

public enum CodebaseMemoryMcpStatus
{
    Stopped,
    Starting,
    Running,
    Failed,
    Disabled
}
