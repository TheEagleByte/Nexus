using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Hubs;

[Collection("NexusHub")]
public class NexusHubRegistrationTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<IHubCallerClients> _clientsMock = new();
    private readonly Mock<ISingleClientProxy> _callerMock = new();
    private readonly NexusHub _hub;

    public NexusHubRegistrationTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _loggerMock.Object);

        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Groups")!.SetValue(_hub, _groupsMock.Object);
        hubType.GetProperty("Clients")!.SetValue(_hub, _clientsMock.Object);
    }

    public void Dispose()
    {
        NexusHub.ClearConnectionsForTesting();
        GC.SuppressFinalize(this);
    }

    private void SetupConnectedSpoke(string connectionId, Guid spokeId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString($"?spokeId={spokeId}");

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = httpContext });

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(features);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Simulate OnConnectedAsync to populate the map
        _hub.OnConnectedAsync().GetAwaiter().GetResult();
    }

    private SpokeRegistration CreateRegistration(string name = "test-spoke") => new(
        Name: name,
        Capabilities: ["code", "test"],
        Os: "linux",
        Architecture: "x64",
        Config: new SpokeConfigDto("plan_review", 5, 30),
        Profile: new SpokeProfileDto(
            "Test Workstation", "Ubuntu 24.04", [], null, ["github"], "Test spoke"),
        Metadata: new Dictionary<string, string> { ["env"] = "test" }
    );

    [Fact]
    public async Task RegisterSpoke_ValidConnection_CallsBackWithSpokeInfo()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, default))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId, Name = "test-spoke", Status = SpokeStatus.Online,
                Capabilities = System.Text.Json.JsonSerializer.SerializeToDocument(Array.Empty<string>()),
                Config = System.Text.Json.JsonSerializer.SerializeToDocument(new { }),
                LastSeen = now, CreatedAt = now, UpdatedAt = now
            });

        SetupConnectedSpoke(connectionId, spokeId);

        await _hub.RegisterSpoke(CreateRegistration());

        _callerMock.Verify(c => c.SendCoreAsync(
            "SpokeRegistered",
            It.Is<object?[]>(args => args.Length == 1 && ((SpokeInfo)args[0]!).SpokeId == spokeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterSpoke_ExistingSpoke_UpdatesConfig()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;
        var spoke = new Spoke
        {
            Id = spokeId, Name = "old-name", Status = SpokeStatus.Online,
            Capabilities = System.Text.Json.JsonSerializer.SerializeToDocument(Array.Empty<string>()),
            Config = System.Text.Json.JsonSerializer.SerializeToDocument(new { }),
            LastSeen = now, CreatedAt = now, UpdatedAt = now
        };

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, default))
            .ReturnsAsync(spoke);

        SetupConnectedSpoke(connectionId, spokeId);

        await _hub.RegisterSpoke(CreateRegistration("new-name"));

        _spokeServiceMock.Verify(s => s.UpdateSpokeConfigAsync(
            spokeId, "new-name", It.IsAny<System.Text.Json.JsonDocument>(), default), Times.Once);
    }

    [Fact]
    public async Task RegisterSpoke_UnmappedConnection_ThrowsHubException()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(new FeatureCollection());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        await Assert.ThrowsAsync<HubException>(() => _hub.RegisterSpoke(CreateRegistration()));
    }

    [Fact]
    public async Task Heartbeat_ValidConnection_UpdatesLastSeen()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        var heartbeat = new SpokeHeartbeat(
            spokeId, SpokeStatus.Online, 2,
            new ResourceUsageDto(1024, 45.5, 50000),
            DateTime.UtcNow);

        await _hub.Heartbeat(heartbeat);

        _spokeServiceMock.Verify(s => s.UpdateSpokeHeartbeatAsync(spokeId, default), Times.Once);
        _callerMock.Verify(c => c.SendCoreAsync(
            "HeartbeatAcknowledged",
            It.Is<object?[]>(args => args.Length == 2 && (Guid)args[0]! == spokeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Heartbeat_BusyStatus_UpdatesStatusToBusy()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        var heartbeat = new SpokeHeartbeat(
            spokeId, SpokeStatus.Busy, 5,
            new ResourceUsageDto(2048, 90.0, 50000),
            DateTime.UtcNow);

        await _hub.Heartbeat(heartbeat);

        _spokeServiceMock.Verify(s => s.UpdateSpokeHeartbeatAsync(spokeId, default), Times.Once);
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Busy, default), Times.Once);
    }

    [Fact]
    public async Task Heartbeat_SpokeIdMismatch_ThrowsHubException()
    {
        var spokeId = Guid.NewGuid();
        var wrongSpokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        var heartbeat = new SpokeHeartbeat(
            wrongSpokeId, SpokeStatus.Online, 0,
            new ResourceUsageDto(512, 10.0, 20000),
            DateTime.UtcNow);

        await Assert.ThrowsAsync<HubException>(() => _hub.Heartbeat(heartbeat));
    }

    [Fact]
    public async Task Heartbeat_UnmappedConnection_ThrowsHubException()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(new FeatureCollection());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        var heartbeat = new SpokeHeartbeat(
            Guid.NewGuid(), SpokeStatus.Online, 0,
            new ResourceUsageDto(512, 10.0, 20000),
            DateTime.UtcNow);

        await Assert.ThrowsAsync<HubException>(() => _hub.Heartbeat(heartbeat));
    }

    [Fact]
    public async Task Heartbeat_ServiceThrows_ThrowsHubException()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        _spokeServiceMock
            .Setup(s => s.UpdateSpokeHeartbeatAsync(spokeId, default))
            .ThrowsAsync(new Exception("DB down"));

        var heartbeat = new SpokeHeartbeat(
            spokeId, SpokeStatus.Online, 0,
            new ResourceUsageDto(512, 10.0, 20000),
            DateTime.UtcNow);

        await Assert.ThrowsAsync<HubException>(() => _hub.Heartbeat(heartbeat));
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
