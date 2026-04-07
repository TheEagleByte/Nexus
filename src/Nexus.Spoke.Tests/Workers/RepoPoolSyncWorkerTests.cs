using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class RepoPoolSyncWorkerTests
{
    private readonly Mock<IRepoPoolService> _mockRepoPool = new();
    private readonly SpokeConfiguration _config = new()
    {
        GitProvider = new SpokeConfiguration.GitProviderConfig
        {
            SyncIntervalSeconds = 30
        }
    };

    private RepoPoolSyncWorker CreateWorker() => new(
        _mockRepoPool.Object,
        Options.Create(_config),
        NullLogger<RepoPoolSyncWorker>.Instance);

    [Fact]
    public async Task ExecuteAsync_CallsInitializeOnStartup()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        // Cancel after initialize runs but before sync timer fires
        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await worker.StartAsync(cts.Token);
        // Give the background service time to execute
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InitializeError_DoesNotCrash()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("init failed"));

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Worker should not throw, just log
        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
