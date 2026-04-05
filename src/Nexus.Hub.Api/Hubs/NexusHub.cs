using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Hubs;

public class NexusHub(ISpokeService spokeService, ILogger<NexusHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    private static readonly ConcurrentDictionary<string, Guid> ConnectionToSpokeMap = new();

    private readonly ISpokeService _spokeService = spokeService;
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

    public static Guid? GetSpokeIdByConnection(string connectionId)
        => ConnectionToSpokeMap.TryGetValue(connectionId, out var spokeId) ? spokeId : null;

    public static IReadOnlyDictionary<string, Guid> GetActiveConnections() => ConnectionToSpokeMap;

    internal static void ClearConnectionsForTesting() => ConnectionToSpokeMap.Clear();
}
