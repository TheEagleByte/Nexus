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
public class NexusHubMessageTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<IConversationService> _conversationServiceMock = new();
    private readonly Mock<IPendingActionService> _pendingActionServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<IHubCallerClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _allClientsMock = new();
    private readonly Mock<IHubContext<NexusHub>> _hubContextMock = new();
    private readonly Mock<IHubClients> _hubClientsMock = new();
    private readonly Mock<ISingleClientProxy> _groupClientMock = new();
    private readonly Mock<IClientProxy> _dispatchAllClientMock = new();
    private readonly NexusHub _hub;

    public NexusHubMessageTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _jobServiceMock.Object, _projectServiceMock.Object, _messageServiceMock.Object, _conversationServiceMock.Object, _pendingActionServiceMock.Object, _loggerMock.Object);

        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);
        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupClientMock.Object);
        _hubClientsMock.Setup(c => c.All).Returns(_dispatchAllClientMock.Object);
        _hubContextMock.Setup(c => c.Clients).Returns(_hubClientsMock.Object);
        _jobServiceMock
            .Setup(s => s.ListJobsAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<JobStatus?>(), It.IsAny<JobType?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job>());

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

        _hub.OnConnectedAsync().GetAwaiter().GetResult();
    }

    private void SetupUnmappedContext(string connectionId)
    {
        var features = new FeatureCollection();

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.Features).Returns(features);

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);
    }

    [Fact]
    public async Task MessageFromSpoke_ValidConnection_RecordsAndBroadcasts()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var recorded = new Message
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Direction = MessageDirection.SpokeToUser,
            Content = "hello from spoke",
            Timestamp = DateTimeOffset.UtcNow
        };

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, "hello from spoke", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recorded);

        await _hub.MessageFromSpoke(new SpokeMessage("hello from spoke", null));

        _messageServiceMock.Verify(s => s.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, "hello from spoke", null, It.IsAny<CancellationToken>()), Times.Once);
        _allClientsMock.Verify(c => c.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MessageFromSpoke_WithJobId_PassesJobId()
    {
        var spokeId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var recorded = new Message
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Direction = MessageDirection.SpokeToUser,
            Content = "job output",
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow
        };

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, "job output", jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recorded);

        await _hub.MessageFromSpoke(new SpokeMessage("job output", jobId));

        _messageServiceMock.Verify(s => s.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, "job output", jobId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MessageFromSpoke_UnmappedConnection_ThrowsHubException()
    {
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupUnmappedContext(connectionId);

        await Assert.ThrowsAsync<HubException>(
            () => _hub.MessageFromSpoke(new SpokeMessage("hello", null)));

        _messageServiceMock.Verify(s => s.RecordMessageAsync(It.IsAny<Guid>(), It.IsAny<MessageDirection>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchMessageToSpoke_ConnectedSpoke_RecordsAndSends()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var recorded = new Message
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Direction = MessageDirection.UserToSpoke,
            Content = "do this task",
            Timestamp = DateTimeOffset.UtcNow
        };

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "do this task", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recorded);

        var logger = new Mock<ILogger>();

        await NexusHub.DispatchMessageToSpoke(_hubContextMock.Object, _messageServiceMock.Object, logger.Object, spokeId, "do this task");

        _messageServiceMock.Verify(s => s.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "do this task", null, It.IsAny<CancellationToken>()), Times.Once);
        _groupClientMock.Verify(c => c.SendCoreAsync("ReceiveMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatchAllClientMock.Verify(c => c.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchMessageToSpoke_DisconnectedSpoke_ThrowsInvalidOperationException()
    {
        var spokeId = Guid.NewGuid();
        var logger = new Mock<ILogger>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NexusHub.DispatchMessageToSpoke(_hubContextMock.Object, _messageServiceMock.Object, logger.Object, spokeId, "hello"));

        _messageServiceMock.Verify(s => s.RecordMessageAsync(It.IsAny<Guid>(), It.IsAny<MessageDirection>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchMessageToSpoke_WithJobId_PassesJobId()
    {
        var spokeId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var recorded = new Message
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Direction = MessageDirection.UserToSpoke,
            Content = "with job",
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow
        };

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "with job", jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recorded);

        var logger = new Mock<ILogger>();

        await NexusHub.DispatchMessageToSpoke(_hubContextMock.Object, _messageServiceMock.Object, logger.Object, spokeId, "with job", jobId);

        _messageServiceMock.Verify(s => s.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "with job", jobId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
