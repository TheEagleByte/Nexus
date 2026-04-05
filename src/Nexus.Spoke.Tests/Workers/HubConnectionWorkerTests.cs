using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class HubConnectionWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_CallsConnectAsync()
    {
        var mockConnection = new Mock<IHubConnectionService>();
        var worker = new HubConnectionWorker(
            mockConnection.Object,
            NullLogger<HubConnectionWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(cts.Token);
        await Task.Delay(50); // Give time for execute to start
        await worker.StopAsync(CancellationToken.None);

        mockConnection.Verify(c => c.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsDisconnectOnStop()
    {
        var mockConnection = new Mock<IHubConnectionService>();
        var worker = new HubConnectionWorker(
            mockConnection.Object,
            NullLogger<HubConnectionWorker>.Instance);

        using var cts = new CancellationTokenSource();

        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        mockConnection.Verify(c => c.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionFailure_DoesNotCrash()
    {
        var mockConnection = new Mock<IHubConnectionService>();
        mockConnection.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var worker = new HubConnectionWorker(
            mockConnection.Object,
            NullLogger<HubConnectionWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        // Worker survived the connection failure
        mockConnection.Verify(c => c.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
