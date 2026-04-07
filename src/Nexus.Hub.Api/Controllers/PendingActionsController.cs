using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/pending-actions")]
public class PendingActionsController(
    IPendingActionService pendingActionService,
    IHubContext<NexusHub> hubContext,
    ILogger<PendingActionsController> logger) : ControllerBase
{
    private readonly IPendingActionService _pendingActionService = pendingActionService;
    private readonly IHubContext<NexusHub> _hubContext = hubContext;
    private readonly ILogger<PendingActionsController> _logger = logger;

    private static readonly HashSet<string> ValidActions = new(StringComparer.OrdinalIgnoreCase) { "approve", "reject", "respond" };

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] PendingActionType? gateType = null,
        [FromQuery] Guid? spokeId = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? sort = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Offset must be non-negative",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        limit = Math.Clamp(limit, 1, 100);

        // age_desc (default) = oldest first = ascending CreatedAt
        // age_asc = newest first = descending CreatedAt
        var sortAscending = !string.Equals(sort, "age_asc", StringComparison.OrdinalIgnoreCase);

        var actions = await _pendingActionService.ListAsync(spokeId, projectId, gateType, status: null, limit, offset, sortAscending, cancellationToken);
        var total = await _pendingActionService.CountAsync(spokeId, projectId, gateType, status: null, cancellationToken);

        var response = new PendingActionListResponse
        {
            PendingActions = actions.Select(MapToResponse).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAsync(Guid id, [FromBody] ResolvePendingActionRequest request, CancellationToken cancellationToken)
    {
        if (!ValidActions.Contains(request.Action))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = $"Invalid action '{request.Action}'. Must be one of: approve, reject, respond",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var resolved = await _pendingActionService.ResolveAsync(id, request.Action, request.Notes, request.Modifications, cancellationToken);

        // Broadcast SignalR event
        try
        {
            var resolvedEvent = new PendingActionResolvedEvent(
                resolved.Id,
                resolved.SpokeId,
                request.Action.ToLowerInvariant(),
                request.Notes,
                resolved.ResolvedAt ?? DateTimeOffset.UtcNow
            );
            await _hubContext.Clients.All.SendAsync("PendingActionResolved", resolvedEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast PendingActionResolved event for action {ActionId}", id);
        }

        return Ok(new ResolvePendingActionResponse
        {
            Id = resolved.Id,
            Status = resolved.Status,
            Action = request.Action.ToLowerInvariant(),
            ResolvedAt = resolved.ResolvedAt ?? DateTimeOffset.UtcNow,
            Metadata = resolved.Metadata
        });
    }

    private static PendingActionResponse MapToResponse(PendingAction action)
    {
        string? summary = null;
        string? description = null;

        if (action.Metadata is not null)
        {
            var root = action.Metadata.RootElement;
            if (root.TryGetProperty("summary", out var summaryEl))
                summary = summaryEl.ValueKind == System.Text.Json.JsonValueKind.String
                    ? summaryEl.GetString()
                    : summaryEl.ToString();
            if (root.TryGetProperty("description", out var descEl))
                description = descEl.ValueKind == System.Text.Json.JsonValueKind.String
                    ? descEl.GetString()
                    : descEl.ToString();
        }

        return new PendingActionResponse
        {
            Id = action.Id,
            SpokeId = action.SpokeId,
            SpokeName = action.Spoke?.Name ?? string.Empty,
            ProjectId = action.ProjectId,
            ExternalKey = action.Project?.ExternalKey,
            GateType = action.Type,
            Summary = summary,
            Description = description,
            CreatedAt = action.CreatedAt,
            Age = FormatAge(DateTimeOffset.UtcNow - action.CreatedAt),
            Metadata = action.Metadata
        };
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalMinutes < 1)
            return "< 1m";
        if (age.TotalHours < 1)
            return $"{(int)age.TotalMinutes}m";
        if (age.TotalDays < 1)
            return $"{(int)age.TotalHours}h {age.Minutes}m";
        return $"{(int)age.TotalDays}d {age.Hours}h";
    }
}
