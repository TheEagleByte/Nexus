namespace Nexus.Spoke.Services;

public interface IHubConnectionService
{
    bool IsConnected { get; }
    Guid? SpokeId { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);
    void OnReceived<T>(string method, Func<T, Task> handler);
    void OnReceived<T1, T2>(string method, Func<T1, T2, Task> handler);
}
