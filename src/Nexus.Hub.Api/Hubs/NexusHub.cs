using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Domain.Entities;
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
        await _spokeService.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Online);

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

    public static Guid? GetSpokeIdByConnection(string connectionId)
        => ConnectionToSpokeMap.TryGetValue(connectionId, out var spokeId) ? spokeId : null;

    public static IReadOnlyDictionary<string, Guid> GetActiveConnections() => ConnectionToSpokeMap;
}
