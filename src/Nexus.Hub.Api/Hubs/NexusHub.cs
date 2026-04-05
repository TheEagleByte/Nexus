using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Hubs;

public class NexusHub(ISpokeService spokeService, IJobService jobService, IProjectService projectService, IMessageService messageService, ILogger<NexusHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    private static readonly ConcurrentDictionary<string, Guid> ConnectionToSpokeMap = new();

    private readonly ISpokeService _spokeService = spokeService;
    private readonly IJobService _jobService = jobService;
    private readonly IProjectService _projectService = projectService;
    private readonly IMessageService _messageService = messageService;
    private readonly ILogger<NexusHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var spokeIdRaw = httpContext?.Request.Query["spokeId"].FirstOrDefault();

        if (!Guid.TryParse(spokeIdRaw, out var spokeId))
        {
            _logger.LogWarning("Connection {ConnectionId} rejected: missing or invalid spokeId", Context.ConnectionId);
            Context.Abort();
            return;
        }

        ConnectionToSpokeMap[Context.ConnectionId] = spokeId;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"spoke-{spokeId}");

        try
        {
            await _spokeService.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Online);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update spoke {SpokeId} status on connect; rolling back", spokeId);
            ConnectionToSpokeMap.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"spoke-{spokeId}");
            Context.Abort();
            return;
        }

        _logger.LogInformation("Spoke {SpokeId} connected (connection {ConnectionId})", spokeId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToSpokeMap.TryRemove(Context.ConnectionId, out var spokeId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"spoke-{spokeId}");

            try
            {
                await _spokeService.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update spoke {SpokeId} status on disconnect", spokeId);
            }

            _logger.LogInformation("Spoke {SpokeId} disconnected (connection {ConnectionId})", spokeId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("Connection {ConnectionId} disconnected but was not in spoke map", Context.ConnectionId);
        }

        if (exception is not null)
        {
            _logger.LogError(exception, "Connection {ConnectionId} disconnected with error", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterSpoke(SpokeRegistration registration)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("RegisterSpoke called from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established. Connect with a valid spokeId first.");
        }

        var capabilities = JsonSerializer.SerializeToDocument(registration.Capabilities);
        var config = JsonSerializer.SerializeToDocument(new
        {
            registration.Config.ApprovalMode,
            registration.Config.MaxConcurrentJobs,
            registration.Config.HeartbeatIntervalSeconds,
            registration.Os,
            registration.Architecture,
            Metadata = registration.Metadata ?? new Dictionary<string, string>()
        });

        JsonDocument? profile = registration.Profile is not null
            ? JsonSerializer.SerializeToDocument(registration.Profile)
            : null;

        Spoke? spoke;
        try
        {
            spoke = await _spokeService.GetSpokeAsync(spokeId);
            await _spokeService.UpdateSpokeConfigAsync(spokeId, registration.Name, config);
            spoke = await _spokeService.GetSpokeAsync(spokeId);
        }
        catch (NotFoundException)
        {
            spoke = await _spokeService.RegisterSpokeAsync(registration.Name, capabilities, config, profile);

            // Remap connection to the newly assigned spoke ID
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"spoke-{spokeId}");
            spokeId = spoke.Id;
            ConnectionToSpokeMap[Context.ConnectionId] = spokeId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"spoke-{spokeId}");
        }

        _logger.LogInformation(
            "Spoke {SpokeId} registered: {SpokeName} (CorrelationId: {CorrelationId})",
            spokeId, registration.Name, correlationId);

        await Clients.Caller.SendAsync("SpokeRegistered", new SpokeInfo(
            spokeId,
            registration.Name,
            SpokeStatus.Online,
            spoke?.CreatedAt ?? DateTimeOffset.UtcNow
        ));
    }

    public async Task Heartbeat(SpokeHeartbeat heartbeat)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("Heartbeat from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established. Connect with a valid spokeId first.");
        }

        if (heartbeat.SpokeId != spokeId)
        {
            _logger.LogWarning(
                "Heartbeat spokeId mismatch: connection mapped to {MappedSpokeId} but heartbeat claims {HeartbeatSpokeId} (CorrelationId: {CorrelationId})",
                spokeId, heartbeat.SpokeId, correlationId);
            throw new HubException("SpokeId mismatch. Heartbeat spokeId does not match connection.");
        }

        try
        {
            await _spokeService.UpdateSpokeHeartbeatAsync(spokeId);
            await _spokeService.UpdateSpokeStatusAsync(spokeId, heartbeat.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat for spoke {SpokeId} (CorrelationId: {CorrelationId})",
                spokeId, correlationId);
            throw new HubException("Failed to process heartbeat.");
        }

        _logger.LogDebug("Heartbeat processed for spoke {SpokeId} (CorrelationId: {CorrelationId})",
            spokeId, correlationId);

        await Clients.Caller.SendAsync("HeartbeatAcknowledged", spokeId, DateTimeOffset.UtcNow);
    }

    public async Task ReportJobStatusChanged(JobStatusChangedEvent evt)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("ReportJobStatusChanged from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established.");
        }

        if (evt.SpokeId != spokeId)
        {
            _logger.LogWarning("ReportJobStatusChanged spokeId mismatch: mapped {MappedSpokeId}, event claims {EventSpokeId} (CorrelationId: {CorrelationId})",
                spokeId, evt.SpokeId, correlationId);
            throw new HubException("SpokeId mismatch.");
        }

        await _jobService.UpdateJobStatusAsync(evt.JobId, evt.NewStatus, evt.Summary);

        await Clients.All.SendAsync("JobStatusChanged", evt);

        _logger.LogInformation(
            "Job {JobId} status changed: {PreviousStatus} → {NewStatus} (spoke: {SpokeId}, CorrelationId: {CorrelationId})",
            evt.JobId, evt.PreviousStatus, evt.NewStatus, spokeId, correlationId);
    }

    public async Task StreamJobOutput(JobOutputChunk chunk)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("StreamJobOutput from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established.");
        }

        if (chunk.SpokeId != spokeId)
        {
            _logger.LogWarning("StreamJobOutput spokeId mismatch: mapped {MappedSpokeId}, chunk claims {ChunkSpokeId} (CorrelationId: {CorrelationId})",
                spokeId, chunk.SpokeId, correlationId);
            throw new HubException("SpokeId mismatch.");
        }

        var persisted = await _jobService.RecordJobOutputAsync(chunk.JobId, chunk.Content, chunk.StreamType);

        var broadcastChunk = new JobOutputChunk(chunk.JobId, chunk.SpokeId, persisted.Sequence, persisted.Content, persisted.StreamType, persisted.Timestamp);
        await Clients.All.SendAsync("JobOutputReceived", broadcastChunk);

        _logger.LogDebug("Job {JobId} output chunk {Sequence} received (spoke: {SpokeId}, CorrelationId: {CorrelationId})",
            chunk.JobId, chunk.Sequence, spokeId, correlationId);
    }

    public async Task ReportProjectStatusChanged(Guid projectId, ProjectStatus newStatus)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("ReportProjectStatusChanged from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established.");
        }

        await _projectService.UpdateProjectStatusAsync(projectId, newStatus);

        await Clients.All.SendAsync("ProjectUpdated", new { ProjectId = projectId, Status = newStatus, Timestamp = DateTimeOffset.UtcNow });

        _logger.LogInformation(
            "Project {ProjectId} status changed to {NewStatus} (spoke: {SpokeId}, CorrelationId: {CorrelationId})",
            projectId, newStatus, spokeId, correlationId);
    }

    public async Task MessageFromSpoke(SpokeMessage message)
    {
        var correlationId = Guid.NewGuid();

        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            _logger.LogWarning("MessageFromSpoke from unmapped connection {ConnectionId} (CorrelationId: {CorrelationId})",
                Context.ConnectionId, correlationId);
            throw new HubException("Connection not established. Connect with a valid spokeId first.");
        }

        var recorded = await _messageService.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, message.Content, message.JobId);

        await Clients.All.SendAsync("MessageReceived", new
        {
            recorded.Id,
            recorded.SpokeId,
            Direction = recorded.Direction.ToString(),
            recorded.Content,
            recorded.JobId,
            recorded.Timestamp
        });

        _logger.LogInformation(
            "Message {MessageId} received from spoke {SpokeId} (CorrelationId: {CorrelationId})",
            recorded.Id, spokeId, correlationId);
    }

    public static async Task DispatchMessageToSpoke(
        IHubContext<NexusHub> hubContext,
        IMessageService messageService,
        ILogger logger,
        Guid spokeId, string content, Guid? jobId = null)
    {
        var correlationId = Guid.NewGuid();

        var isConnected = ConnectionToSpokeMap.Values.Contains(spokeId);
        if (!isConnected)
        {
            logger.LogWarning(
                "Message dispatch failed: spoke {SpokeId} is not connected (CorrelationId: {CorrelationId})",
                spokeId, correlationId);
            throw new InvalidOperationException($"Spoke {spokeId} is not connected.");
        }

        var recorded = await messageService.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, content, jobId);

        await hubContext.Clients.Group($"spoke-{spokeId}").SendAsync("ReceiveMessage", new
        {
            recorded.Id,
            recorded.SpokeId,
            Direction = recorded.Direction.ToString(),
            recorded.Content,
            recorded.JobId,
            recorded.Timestamp
        });

        await hubContext.Clients.All.SendAsync("MessageReceived", new
        {
            recorded.Id,
            recorded.SpokeId,
            Direction = recorded.Direction.ToString(),
            recorded.Content,
            recorded.JobId,
            recorded.Timestamp
        });

        logger.LogInformation(
            "Message {MessageId} dispatched to spoke {SpokeId} (CorrelationId: {CorrelationId})",
            recorded.Id, spokeId, correlationId);
    }

    public static async Task DispatchJobAssignment(
        IHubContext<NexusHub> hubContext,
        IJobService jobService,
        ILogger logger,
        Guid spokeId, Guid projectId, JobType type, string context,
        bool requireApproval, Dictionary<string, object>? customFields = null)
    {
        var correlationId = Guid.NewGuid();

        var isConnected = ConnectionToSpokeMap.Values.Contains(spokeId);
        if (!isConnected)
        {
            logger.LogWarning(
                "Job assignment failed: spoke {SpokeId} is not connected (CorrelationId: {CorrelationId})",
                spokeId, correlationId);
            throw new InvalidOperationException($"Spoke {spokeId} is not connected.");
        }

        var contextDoc = JsonSerializer.SerializeToDocument(context);
        var job = await jobService.CreateJobAsync(spokeId, projectId, type, requireApproval, contextDoc);

        var assignment = new JobAssignment(
            JobId: job.Id,
            ProjectId: projectId,
            Type: type,
            Context: context,
            Parameters: new JobParameters(customFields),
            RequireApproval: requireApproval,
            AssignedAt: DateTimeOffset.UtcNow
        );

        try
        {
            await hubContext.Clients.Group($"spoke-{spokeId}").SendAsync("AssignJob", assignment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send job {JobId} to spoke {SpokeId}; job stranded in {Status} (CorrelationId: {CorrelationId})",
                job.Id, spokeId, job.Status, correlationId);
            throw;
        }

        logger.LogInformation(
            "Job {JobId} assigned to spoke {SpokeId} (project: {ProjectId}, type: {JobType}, approval: {RequireApproval}, CorrelationId: {CorrelationId})",
            job.Id, spokeId, projectId, type, requireApproval, correlationId);
    }

    public static Guid? GetSpokeIdByConnection(string connectionId)
        => ConnectionToSpokeMap.TryGetValue(connectionId, out var spokeId) ? spokeId : null;

    public static IReadOnlyDictionary<string, Guid> GetActiveConnections() => ConnectionToSpokeMap;

    internal static void ClearConnectionsForTesting() => ConnectionToSpokeMap.Clear();
}
