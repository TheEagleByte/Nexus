using System.Text.Json;
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
public class NexusHubJobAssignmentTests : IDisposable
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IJobService> _jobServiceMock = new();
    private readonly Mock<ILogger<NexusHub>> _loggerMock = new();
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<IHubCallerClients> _clientsMock = new();
    private readonly Mock<ISingleClientProxy> _groupClientMock = new();
    private readonly NexusHub _hub;

    public NexusHubJobAssignmentTests()
    {
        _hub = new NexusHub(_spokeServiceMock.Object, _jobServiceMock.Object, _loggerMock.Object);

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
    public async Task SendJobAssignment_ConnectedSpoke_SendsToGroup()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = JobType.Implement,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, false, It.IsAny<JsonDocument>(), default))
            .ReturnsAsync(job);

        _clientsMock
            .Setup(c => c.Group($"spoke-{spokeId}"))
            .Returns(_groupClientMock.Object);

        await _hub.SendJobAssignment(spokeId, projectId, JobType.Implement, "Implement feature X", false);

        _jobServiceMock.Verify(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, false, It.IsAny<JsonDocument>(), default), Times.Once);
        _groupClientMock.Verify(c => c.SendCoreAsync(
            "AssignJob",
            It.Is<object?[]>(args => args.Length == 1 && ((JobAssignment)args[0]!).JobId == job.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendJobAssignment_WithApproval_CreatesAwaitingApprovalJob()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";

        SetupConnectedSpoke(connectionId, spokeId);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = JobType.Implement,
            Status = JobStatus.AwaitingApproval,
            ApprovalRequired = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, true, It.IsAny<JsonDocument>(), default))
            .ReturnsAsync(job);

        _clientsMock
            .Setup(c => c.Group($"spoke-{spokeId}"))
            .Returns(_groupClientMock.Object);

        await _hub.SendJobAssignment(spokeId, projectId, JobType.Implement, "Implement feature X", true);

        _jobServiceMock.Verify(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, true, It.IsAny<JsonDocument>(), default), Times.Once);
        _groupClientMock.Verify(c => c.SendCoreAsync(
            "AssignJob",
            It.Is<object?[]>(args => args.Length == 1 && ((JobAssignment)args[0]!).RequireApproval),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendJobAssignment_DisconnectedSpoke_ThrowsHubException()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        // No spoke connected — map is empty
        await Assert.ThrowsAsync<HubException>(() =>
            _hub.SendJobAssignment(spokeId, projectId, JobType.Implement, "context", false));

        _jobServiceMock.Verify(s => s.CreateJobAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<JobType>(), It.IsAny<bool>(), It.IsAny<JsonDocument>(), default), Times.Never);
    }

    [Fact]
    public async Task SendJobAssignment_PayloadIncludesContext()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        var contextText = "Implement ticket NEX-99: Add caching layer";

        SetupConnectedSpoke(connectionId, spokeId);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = JobType.Implement,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, false, It.IsAny<JsonDocument>(), default))
            .ReturnsAsync(job);

        _clientsMock
            .Setup(c => c.Group($"spoke-{spokeId}"))
            .Returns(_groupClientMock.Object);

        await _hub.SendJobAssignment(spokeId, projectId, JobType.Implement, contextText, false);

        _groupClientMock.Verify(c => c.SendCoreAsync(
            "AssignJob",
            It.Is<object?[]>(args =>
                args.Length == 1 &&
                ((JobAssignment)args[0]!).Context == contextText &&
                ((JobAssignment)args[0]!).ProjectId == projectId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendJobAssignment_WithCustomFields_IncludedInPayload()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var connectionId = $"conn-{Guid.NewGuid()}";
        var customFields = new Dictionary<string, object> { ["branch"] = "feature/caching" };

        SetupConnectedSpoke(connectionId, spokeId);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = JobType.Implement,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _jobServiceMock
            .Setup(s => s.CreateJobAsync(spokeId, projectId, JobType.Implement, false, It.IsAny<JsonDocument>(), default))
            .ReturnsAsync(job);

        _clientsMock
            .Setup(c => c.Group($"spoke-{spokeId}"))
            .Returns(_groupClientMock.Object);

        await _hub.SendJobAssignment(spokeId, projectId, JobType.Implement, "context", false, customFields);

        _groupClientMock.Verify(c => c.SendCoreAsync(
            "AssignJob",
            It.Is<object?[]>(args =>
                args.Length == 1 &&
                ((JobAssignment)args[0]!).Parameters.CustomFields != null &&
                ((JobAssignment)args[0]!).Parameters.CustomFields!.ContainsKey("branch")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
