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
public class NexusHubTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly NexusHub _hub;

    public NexusHubTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _loggerMock.Object);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Groups")!.SetValue(_hub, _groupsMock.Object);
    }

    public void Dispose()
    {
        NexusHub.ClearConnectionsForTesting();
        GC.SuppressFinalize(this);
    }

    private void SetupContext(string connectionId, Guid? spokeId)
    {
        var httpContext = new DefaultHttpContext();
        if (spokeId.HasValue)
        {
            httpContext.Request.QueryString = new QueryString($"?spokeId={spokeId.Value}");
        }

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = httpContext });

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(features);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);
    }

    private void SetupContextWithInvalidSpokeId(string connectionId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?spokeId=not-a-guid");

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = httpContext });

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(features);
        contextMock.Setup(c => c.Abort());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);
    }

    private void SetupDisconnectContext(string connectionId)
    {
        var features = new FeatureCollection();

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(features);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);
    }

    [Fact]
    public async Task OnConnectedAsync_ValidSpokeId_AddsToMapAndGroup()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _hub.OnConnectedAsync();

        Assert.Equal(spokeId, NexusHub.GetSpokeIdByConnection(connectionId));
        _groupsMock.Verify(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()), Times.Once);
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Online, default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_MissingSpokeId_AbortsConnection()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, null);

        await _hub.OnConnectedAsync();

        Assert.Null(NexusHub.GetSpokeIdByConnection(connectionId));
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_InvalidSpokeId_AbortsConnection()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContextWithInvalidSpokeId(connectionId);

        await _hub.OnConnectedAsync();

        Assert.Null(NexusHub.GetSpokeIdByConnection(connectionId));
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_KnownConnection_RemovesFromMapAndUpdatesStatus()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupsMock
            .Setup(g => g.RemoveFromGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _hub.OnConnectedAsync();
        Assert.NotNull(NexusHub.GetSpokeIdByConnection(connectionId));

        // Re-setup context for disconnect (GetHttpContext not needed)
        SetupDisconnectContext(connectionId);
        await _hub.OnDisconnectedAsync(null);

        Assert.Null(NexusHub.GetSpokeIdByConnection(connectionId));
        _groupsMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()), Times.Once);
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline, default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_UnknownConnection_DoesNotThrow()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupDisconnectContext(connectionId);

        await _hub.OnDisconnectedAsync(null);

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_StatusUpdateFails_DoesNotThrow()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupsMock
            .Setup(g => g.RemoveFromGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _spokeServiceMock
            .Setup(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline, default))
            .ThrowsAsync(new Exception("DB down"));

        await _hub.OnConnectedAsync();

        SetupDisconnectContext(connectionId);
        await _hub.OnDisconnectedAsync(null);

        Assert.Null(NexusHub.GetSpokeIdByConnection(connectionId));
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_DoesNotThrow()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupDisconnectContext(connectionId);

        var error = new InvalidOperationException("Transport failed");
        await _hub.OnDisconnectedAsync(error);

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), default), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_StatusUpdateFails_RollsBackAndAborts()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupsMock
            .Setup(g => g.RemoveFromGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _spokeServiceMock
            .Setup(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Online, default))
            .ThrowsAsync(new Exception("DB down"));

        await _hub.OnConnectedAsync();

        Assert.Null(NexusHub.GetSpokeIdByConnection(connectionId));
        _groupsMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetSpokeIdByConnection_UnknownConnectionId_ReturnsNull()
    {
        Assert.Null(NexusHub.GetSpokeIdByConnection("nonexistent-conn-id"));
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
