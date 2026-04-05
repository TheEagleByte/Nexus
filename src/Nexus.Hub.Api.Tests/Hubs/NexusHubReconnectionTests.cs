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
public class NexusHubReconnectionTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<IHubCallerClients> _clientsMock = new();
    private readonly Mock<ISingleClientProxy> _callerMock = new();
    private readonly NexusHub _hub;

    public NexusHubReconnectionTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _jobServiceMock.Object, _projectServiceMock.Object, _messageServiceMock.Object, _loggerMock.Object);

        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
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

    private void SetupContext(string connectionId, Guid spokeId)
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
    public async Task OnConnectedAsync_NoQueuedJobs_DoesNotSendAssignJob()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _hub.OnConnectedAsync();

        _callerMock.Verify(c => c.SendCoreAsync("AssignJob", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_WithQueuedJobs_ReplaysAllQueuedJobs()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var queuedJobs = new List<Job>
        {
            new() { Id = Guid.NewGuid(), SpokeId = spokeId, ProjectId = projectId, Type = JobType.Implement, Status = JobStatus.Queued, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), SpokeId = spokeId, ProjectId = projectId, Type = JobType.Test, Status = JobStatus.Queued, CreatedAt = DateTimeOffset.UtcNow }
        };

        _jobServiceMock
            .Setup(s => s.ListJobsAsync(spokeId, null, JobStatus.Queued, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queuedJobs);

        await _hub.OnConnectedAsync();

        _callerMock.Verify(c => c.SendCoreAsync("AssignJob", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OnConnectedAsync_ReconnectionAfterDisconnect_ReplaysQueuedJobs()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conn1 = $"conn-{Guid.NewGuid()}";

        // First connection
        SetupContext(conn1, spokeId);
        _groupsMock
            .Setup(g => g.AddToGroupAsync(conn1, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupsMock
            .Setup(g => g.RemoveFromGroupAsync(conn1, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _hub.OnConnectedAsync();

        // Disconnect
        SetupDisconnectContext(conn1);
        await _hub.OnDisconnectedAsync(null);

        Assert.Null(NexusHub.GetSpokeIdByConnection(conn1));

        // Reconnect with new connection
        var conn2 = $"conn-{Guid.NewGuid()}";
        SetupContext(conn2, spokeId);
        _groupsMock
            .Setup(g => g.AddToGroupAsync(conn2, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var queuedJob = new Job
        {
            Id = Guid.NewGuid(), SpokeId = spokeId, ProjectId = projectId,
            Type = JobType.Implement, Status = JobStatus.Queued, CreatedAt = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.ListJobsAsync(spokeId, null, JobStatus.Queued, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { queuedJob });

        await _hub.OnConnectedAsync();

        Assert.Equal(spokeId, NexusHub.GetSpokeIdByConnection(conn2));
        _callerMock.Verify(c => c.SendCoreAsync("AssignJob", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_ReplayFailure_DoesNotAbortConnection()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupContext(connectionId, spokeId);

        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jobServiceMock
            .Setup(s => s.ListJobsAsync(spokeId, null, JobStatus.Queued, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB down"));

        await _hub.OnConnectedAsync();

        // Connection should still be established despite replay failure
        Assert.Equal(spokeId, NexusHub.GetSpokeIdByConnection(connectionId));
    }

    [Fact]
    public async Task RegisterSpoke_IncludesReconnectionPolicy()
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

        SetupContext(connectionId, spokeId);
        _groupsMock
            .Setup(g => g.AddToGroupAsync(connectionId, $"spoke-{spokeId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _hub.OnConnectedAsync();

        var registration = new SpokeRegistration(
            "test-spoke", ["code"], "linux", "x64",
            new SpokeConfigDto("plan_review", 5, 30), null, null);

        await _hub.RegisterSpoke(registration);

        object? capturedPayload = null;
        _callerMock.Verify(c => c.SendCoreAsync(
            "SpokeRegistered",
            It.Is<object?[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _callerMock.Invocations
            .Where(i => i.Method.Name == "SendCoreAsync" && (string)i.Arguments[0] == "SpokeRegistered")
            .ToList()
            .ForEach(i => capturedPayload = ((object?[])i.Arguments[1])[0]);

        Assert.NotNull(capturedPayload);
        var payloadType = capturedPayload!.GetType();
        Assert.NotNull(payloadType.GetProperty("ReconnectionPolicy"));
        var infoValue = payloadType.GetProperty("Info")?.GetValue(capturedPayload);
        Assert.NotNull(infoValue);
        var spokeInfo = Assert.IsType<SpokeInfo>(infoValue);
        Assert.Equal(spokeId, spokeInfo.SpokeId);
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
