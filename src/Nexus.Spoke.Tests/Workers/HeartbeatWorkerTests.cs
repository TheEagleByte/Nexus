using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class HeartbeatWorkerTests
{
    private readonly Mock<IHubConnectionService> _mockConnection = new();
    private readonly Mock<ResourceMonitor> _mockResourceMonitor = new();
    private readonly SpokeConfiguration _config = new()
    {
        Spoke = new SpokeConfiguration.SpokeIdentityConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Spoke"
        },
        Approval = new SpokeConfiguration.ApprovalConfig
        {
            HeartbeatIntervalSeconds = 30
        }
    };

    private HeartbeatWorker CreateWorker(
        ILogger<HeartbeatWorker>? logger = null,
        IRepoPoolService? repoPool = null,
        ICodebaseMemoryMcpService? mcpService = null)
    {
        _mockResourceMonitor.Setup(r => r.GetCurrentUsage())
            .Returns(new ResourceUsageDto(512, 25.0, 10240));
        _mockConnection.Setup(c => c.SpokeId).Returns(Guid.NewGuid());

        return new HeartbeatWorker(
            _mockConnection.Object,
            Options.Create(_config),
            _mockResourceMonitor.Object,
            logger ?? NullLogger<HeartbeatWorker>.Instance,
            repoPool,
            mcpService);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenConnected_SendsHeartbeat()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var worker = CreateWorker();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        _mockConnection.Verify(c => c.SendAsync(
            "Heartbeat",
            It.IsAny<SpokeHeartbeat>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendHeartbeatAsync_IncludesResourceUsage()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var worker = CreateWorker();

        SpokeHeartbeat? captured = null;
        _mockConnection.Setup(c => c.SendAsync("Heartbeat", It.IsAny<SpokeHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<string, SpokeHeartbeat, CancellationToken>((_, hb, _) => captured = hb)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        Assert.NotNull(captured);
        Assert.Equal(512, captured.ResourceUsage.MemoryUsageMb);
        Assert.Equal(25.0, captured.ResourceUsage.CpuUsagePercent);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsHeartbeat_WhenNotConnected()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(false);
        var worker = CreateWorker();

        // Use short interval for testing
        _config.Approval.HeartbeatIntervalSeconds = 1;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        await Task.Delay(1500);
        await worker.StopAsync(CancellationToken.None);

        _mockConnection.Verify(c => c.SendAsync(
            "Heartbeat",
            It.IsAny<SpokeHeartbeat>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendHeartbeatAsync_NoAck_IncrementsMissedCount()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var mockLogger = new Mock<ILogger<HeartbeatWorker>>();
        var worker = CreateWorker(mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        // Verify warning was logged about missed ack
        mockLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("not received")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task SendHeartbeatAsync_ThreeMissed_LogsError()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var mockLogger = new Mock<ILogger<HeartbeatWorker>>();
        var worker = CreateWorker(mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Miss 3 consecutive heartbeats
        await worker.SendHeartbeatAsync(cts.Token);
        await worker.SendHeartbeatAsync(cts.Token);
        await worker.SendHeartbeatAsync(cts.Token);

        // Verify error was logged
        mockLogger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("unreachable")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithRepoPool_IncludesMetadata()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var mockRepoPool = new Mock<IRepoPoolService>();
        mockRepoPool.Setup(r => r.GetSyncStates())
            .Returns(new Dictionary<string, RepoSyncState>
            {
                ["my-repo"] = new("my-repo", RepoSyncStatus.Synced, DateTimeOffset.UtcNow, null)
            });

        SpokeHeartbeat? captured = null;
        _mockConnection.Setup(c => c.SendAsync("Heartbeat", It.IsAny<SpokeHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<string, SpokeHeartbeat, CancellationToken>((_, hb, _) => captured = hb)
            .Returns(Task.CompletedTask);

        var worker = CreateWorker(repoPool: mockRepoPool.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        Assert.NotNull(captured);
        Assert.NotNull(captured.Metadata);
        Assert.True(captured.Metadata.ContainsKey("repo:my-repo"));
        Assert.Contains("Synced", captured.Metadata["repo:my-repo"]);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithoutRepoPool_NoMetadata()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);

        SpokeHeartbeat? captured = null;
        _mockConnection.Setup(c => c.SendAsync("Heartbeat", It.IsAny<SpokeHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<string, SpokeHeartbeat, CancellationToken>((_, hb, _) => captured = hb)
            .Returns(Task.CompletedTask);

        var worker = CreateWorker();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        Assert.NotNull(captured);
        Assert.Null(captured.Metadata);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithMcpService_IncludesMcpMetadata()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var mockMcp = new Mock<ICodebaseMemoryMcpService>();
        mockMcp.Setup(m => m.GetStatus()).Returns(CodebaseMemoryMcpStatus.Running);
        mockMcp.Setup(m => m.GetEndpoint()).Returns("http://localhost:3500");

        SpokeHeartbeat? captured = null;
        _mockConnection.Setup(c => c.SendAsync("Heartbeat", It.IsAny<SpokeHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<string, SpokeHeartbeat, CancellationToken>((_, hb, _) => captured = hb)
            .Returns(Task.CompletedTask);

        var worker = CreateWorker(mcpService: mockMcp.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        Assert.NotNull(captured);
        Assert.NotNull(captured.Metadata);
        Assert.True(captured.Metadata.ContainsKey("mcp:codebase-memory"));
        Assert.Contains("Running", captured.Metadata["mcp:codebase-memory"]);
        Assert.Contains("http://localhost:3500", captured.Metadata["mcp:codebase-memory"]);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithMcpServiceNotRunning_IncludesStatusOnly()
    {
        _mockConnection.Setup(c => c.IsConnected).Returns(true);
        var mockMcp = new Mock<ICodebaseMemoryMcpService>();
        mockMcp.Setup(m => m.GetStatus()).Returns(CodebaseMemoryMcpStatus.Stopped);
        mockMcp.Setup(m => m.GetEndpoint()).Returns((string?)null);

        SpokeHeartbeat? captured = null;
        _mockConnection.Setup(c => c.SendAsync("Heartbeat", It.IsAny<SpokeHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<string, SpokeHeartbeat, CancellationToken>((_, hb, _) => captured = hb)
            .Returns(Task.CompletedTask);

        var worker = CreateWorker(mcpService: mockMcp.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.SendHeartbeatAsync(cts.Token);

        Assert.NotNull(captured);
        Assert.NotNull(captured.Metadata);
        Assert.Equal("Stopped", captured.Metadata["mcp:codebase-memory"]);
    }
}
