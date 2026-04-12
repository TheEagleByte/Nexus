using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class CodebaseMemoryMcpServiceTests : IDisposable
{
    private readonly SpokeConfiguration _config = new()
    {
        Workspace = new SpokeConfiguration.WorkspaceConfig
        {
            BaseDirectory = Path.Combine(Path.GetTempPath(), $"nexus-mcp-test-{Guid.NewGuid():N}")
        },
        CodebaseMemoryMcp = new SpokeConfiguration.CodebaseMemoryMcpConfig
        {
            Enabled = true,
            Port = 13500,
            HealthCheckIntervalSeconds = 30,
            StartupTimeoutSeconds = 5
        }
    };

    private CodebaseMemoryMcpService CreateService() => new(
        Options.Create(_config),
        NullLogger<CodebaseMemoryMcpService>.Instance);

    public void Dispose()
    {
        var dir = _config.Workspace.BaseDirectory;
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_SetsStatusToDisabled()
    {
        _config.CodebaseMemoryMcp.Enabled = false;
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(CodebaseMemoryMcpStatus.Disabled, service.GetStatus());
    }

    [Fact]
    public void IsHealthy_WhenStopped_ReturnsFalse()
    {
        using var service = CreateService();

        Assert.False(service.IsHealthy());
    }

    [Fact]
    public void GetEndpoint_WhenNotRunning_ReturnsNull()
    {
        using var service = CreateService();

        Assert.Null(service.GetEndpoint());
    }

    [Fact]
    public void GetStatus_InitialState_IsStopped()
    {
        using var service = CreateService();

        Assert.Equal(CodebaseMemoryMcpStatus.Stopped, service.GetStatus());
    }

    [Fact]
    public async Task StartAsync_WithInvalidCommand_SetsStatusToFailed()
    {
        _config.CodebaseMemoryMcp.NpxCommand = "nonexistent-command-that-does-not-exist-12345";
        _config.CodebaseMemoryMcp.StartupTimeoutSeconds = 10;
        using var service = CreateService();

        // Ensure repos directory exists so the service gets past dir creation
        Directory.CreateDirectory(Path.Combine(_config.Workspace.BaseDirectory, "repos"));

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(CodebaseMemoryMcpStatus.Failed, service.GetStatus());
        Assert.False(service.IsHealthy());
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_CompletesWithoutError()
    {
        using var service = CreateService();

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(CodebaseMemoryMcpStatus.Stopped, service.GetStatus());
    }

    [Fact]
    public async Task ReindexAsync_WhenDisabled_DoesNotThrow()
    {
        _config.CodebaseMemoryMcp.Enabled = false;
        using var service = CreateService();

        await service.ReindexAsync(CancellationToken.None);

        // Should complete without error
    }

    [Fact]
    public async Task ReindexAsync_WhenNotRunning_DoesNotThrow()
    {
        using var service = CreateService();

        await service.ReindexAsync(CancellationToken.None);

        // Should complete without error — status is Stopped, not Running
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_GetEndpoint_ReturnsNull()
    {
        _config.CodebaseMemoryMcp.Enabled = false;
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);

        Assert.Null(service.GetEndpoint());
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_CompletesGracefully()
    {
        using var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Ensure the workspace dirs exist before starting
        Directory.CreateDirectory(Path.Combine(_config.Workspace.BaseDirectory, "repos"));
        Directory.CreateDirectory(Path.Combine(_config.Workspace.BaseDirectory, "mcp-index"));

        // Cancel quickly after start
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Should handle cancellation gracefully without leaving broken state
        try
        {
            await service.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — cancellation during startup is fine
        }

        // Service should still be disposable without errors
    }
}
