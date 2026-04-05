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
public class NexusHubStatusUpdateTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<IProjectService> _projectServiceMock = new();
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<IHubCallerClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _allClientsMock = new();
    private readonly NexusHub _hub;

    public NexusHubStatusUpdateTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _jobServiceMock.Object, _projectServiceMock.Object, _messageServiceMock.Object, _loggerMock.Object);

        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);

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

    [Fact]
    public async Task ReportJobStatusChanged_PersistsAndBroadcasts()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var evt = new JobStatusChangedEvent(
            Guid.NewGuid(), Guid.NewGuid(), spokeId,
            JobStatus.Running, JobStatus.Queued,
            null, null, DateTimeOffset.UtcNow);

        await _hub.ReportJobStatusChanged(evt);

        _jobServiceMock.Verify(s => s.UpdateJobStatusAsync(evt.JobId, JobStatus.Running, null, default), Times.Once);
        _allClientsMock.Verify(c => c.SendCoreAsync("JobStatusChanged",
            It.Is<object?[]>(args => args.Length == 1 && ((JobStatusChangedEvent)args[0]!).JobId == evt.JobId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportJobStatusChanged_WithSummary_PersistsSummary()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var evt = new JobStatusChangedEvent(
            Guid.NewGuid(), Guid.NewGuid(), spokeId,
            JobStatus.Completed, JobStatus.Running,
            "Feature implemented successfully", null, DateTimeOffset.UtcNow);

        await _hub.ReportJobStatusChanged(evt);

        _jobServiceMock.Verify(s => s.UpdateJobStatusAsync(evt.JobId, JobStatus.Completed, "Feature implemented successfully", default), Times.Once);
    }

    [Fact]
    public async Task ReportJobStatusChanged_UnmappedConnection_Throws()
    {
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns($"conn-{Guid.NewGuid()}");
        contextMock.Setup(c => c.Features).Returns(new FeatureCollection());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        var evt = new JobStatusChangedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            JobStatus.Running, JobStatus.Queued,
            null, null, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<HubException>(() => _hub.ReportJobStatusChanged(evt));
    }

    [Fact]
    public async Task StreamJobOutput_PersistsAndBroadcasts()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var jobId = Guid.NewGuid();
        var chunk = new JobOutputChunk(
            jobId, spokeId, 0,
            "Building project...", "stdout", DateTimeOffset.UtcNow);

        var persistedOutput = new Nexus.Hub.Domain.Entities.OutputStream
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Sequence = 0,
            Content = "Building project...",
            Timestamp = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.RecordJobOutputAsync(jobId, "Building project...", "stdout", default))
            .ReturnsAsync(persistedOutput);

        await _hub.StreamJobOutput(chunk);

        _jobServiceMock.Verify(s => s.RecordJobOutputAsync(jobId, "Building project...", "stdout", default), Times.Once);
        _allClientsMock.Verify(c => c.SendCoreAsync("JobOutputReceived",
            It.Is<object?[]>(args => args.Length == 1 && ((JobOutputChunk)args[0]!).JobId == jobId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamJobOutput_UnmappedConnection_Throws()
    {
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns($"conn-{Guid.NewGuid()}");
        contextMock.Setup(c => c.Features).Returns(new FeatureCollection());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        var chunk = new JobOutputChunk(
            Guid.NewGuid(), Guid.NewGuid(), 0,
            "output", "stdout", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<HubException>(() => _hub.StreamJobOutput(chunk));
    }

    [Fact]
    public async Task ReportProjectStatusChanged_PersistsAndBroadcasts()
    {
        var spokeId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        SetupConnectedSpoke(connectionId, spokeId);

        var projectId = Guid.NewGuid();

        await _hub.ReportProjectStatusChanged(projectId, ProjectStatus.Active);

        _projectServiceMock.Verify(s => s.UpdateProjectStatusAsync(projectId, ProjectStatus.Active, default), Times.Once);
        _allClientsMock.Verify(c => c.SendCoreAsync("ProjectUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportProjectStatusChanged_UnmappedConnection_Throws()
    {
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns($"conn-{Guid.NewGuid()}");
        contextMock.Setup(c => c.Features).Returns(new FeatureCollection());

        var hubType = typeof(Microsoft.AspNetCore.SignalR.Hub);
        hubType.GetProperty("Context")!.SetValue(_hub, contextMock.Object);

        await Assert.ThrowsAsync<HubException>(() =>
            _hub.ReportProjectStatusChanged(Guid.NewGuid(), ProjectStatus.Active));
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
