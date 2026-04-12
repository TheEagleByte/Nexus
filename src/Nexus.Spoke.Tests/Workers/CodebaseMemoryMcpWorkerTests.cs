using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class CodebaseMemoryMcpWorkerTests
{
    private readonly Mock<ICodebaseMemoryMcpService> _mockMcpService = new();
    private readonly SpokeConfiguration _config = new()
    {
        CodebaseMemoryMcp = new SpokeConfiguration.CodebaseMemoryMcpConfig
        {
            Enabled = true,
            HealthCheckIntervalSeconds = 30
        }
    };

    private CodebaseMemoryMcpWorker CreateWorker() => new(
        _mockMcpService.Object,
        Options.Create(_config),
        NullLogger<CodebaseMemoryMcpWorker>.Instance);

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ExitsImmediately()
    {
        _config.CodebaseMemoryMcp.Enabled = false;
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockMcpService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CallsStartOnStartup()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        _mockMcpService.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockMcpService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StartupError_ContinuesToHealthCheckLoop()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        _mockMcpService.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("startup failed"));

        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Worker should not throw — startup error is caught and logged
        _mockMcpService.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_CallsMcpServiceStop()
    {
        _config.CodebaseMemoryMcp.Enabled = false; // Skip ExecuteAsync loop
        var worker = CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        _mockMcpService.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_McpServiceStopError_DoesNotThrow()
    {
        _config.CodebaseMemoryMcp.Enabled = false;
        var worker = CreateWorker();

        _mockMcpService.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stop failed"));

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Should not throw
    }
}
