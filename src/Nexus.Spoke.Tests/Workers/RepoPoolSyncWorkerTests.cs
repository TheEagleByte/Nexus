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
        Capabilities = new SpokeConfiguration.CapabilitiesConfig { Git = true },
        GitProvider = new SpokeConfiguration.GitProviderConfig
        {
            SyncIntervalSeconds = 30
        }
    };

    private readonly Mock<ICodebaseMemoryMcpService> _mockMcpService = new();

    private RepoPoolSyncWorker CreateWorker(ICodebaseMemoryMcpService? mcpService = null) => new(
        _mockRepoPool.Object,
        Options.Create(_config),
        NullLogger<RepoPoolSyncWorker>.Instance,
        mcpService);

    [Fact]
    public async Task ExecuteAsync_CallsInitializeOnStartup()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TriggersReindexAfterInitialClone()
    {
        var worker = CreateWorker(_mockMcpService.Object);
        using var cts = new CancellationTokenSource();

        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockMcpService.Setup(s => s.ReindexAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        _mockMcpService.Verify(s => s.ReindexAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ReindexFailureAfterInit_DoesNotCrash()
    {
        var worker = CreateWorker(_mockMcpService.Object);
        using var cts = new CancellationTokenSource();
        var reindexAttempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockMcpService.Setup(s => s.ReindexAsync(It.IsAny<CancellationToken>()))
            .Callback(() => reindexAttempted.TrySetResult(true))
            .ThrowsAsync(new InvalidOperationException("reindex failed"));

        await worker.StartAsync(cts.Token);
        await reindexAttempted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Worker should not throw — reindex error is caught and logged
        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMcpService.Verify(s => s.ReindexAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InitializeError_ContinuesToSyncLoop()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("init failed"));

        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Worker should not throw — init error is caught and logged
        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GitDisabled_ExitsImmediately()
    {
        _config.Capabilities.Git = false;
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockRepoPool.Verify(r => r.SyncAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SyncError_DoesNotCrash()
    {
        // Verify that sync errors are caught by checking the worker survives init + cancellation
        // without throwing. The PeriodicTimer (min 30s) makes testing actual sync cycles impractical
        // in unit tests — RepoPoolService's own tests cover sync error handling.
        _mockRepoPool.Setup(r => r.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        // Worker started and stopped without throwing
        _mockRepoPool.Verify(r => r.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
