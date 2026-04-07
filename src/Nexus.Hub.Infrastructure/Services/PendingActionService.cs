using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class PendingActionService(IPendingActionRepository pendingActionRepository, ILogger<PendingActionService> logger) : IPendingActionService
{
    private readonly IPendingActionRepository _pendingActionRepository = pendingActionRepository;
    private readonly ILogger<PendingActionService> _logger = logger;

    private static readonly HashSet<string> ValidActions = new(StringComparer.OrdinalIgnoreCase) { "approve", "reject", "respond" };

    public async Task<PendingAction> CreateAsync(Guid spokeId, Guid projectId, Guid jobId, PendingActionType type, int priority = 0, JsonDocument? metadata = null, CancellationToken cancellationToken = default)
    {
        if (metadata is not null && metadata.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new Domain.Exceptions.ValidationException("Pending action metadata must be a JSON object.");

        var action = new PendingAction
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            JobId = jobId,
            Type = type,
            Status = PendingActionStatus.Pending,
            Priority = priority,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata
        };

        await _pendingActionRepository.AddAsync(action, cancellationToken);
        _logger.LogInformation("PendingAction {ActionId} created for spoke {SpokeId} (type: {Type}, priority: {Priority})",
            action.Id, spokeId, type, priority);
        return action;
    }

    public async Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var action = await _pendingActionRepository.GetByIdAsync(actionId, cancellationToken);
        if (action is null)
        {
            _logger.LogWarning("PendingAction not found: {ActionId}", actionId);
            throw new Domain.Exceptions.NotFoundException($"PendingAction {actionId} not found");
        }
        return action;
    }

    public Task<List<PendingAction>> ListAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, int limit = 50, int offset = 0, bool sortAscending = true, CancellationToken cancellationToken = default)
        => _pendingActionRepository.ListAsync(spokeId, projectId, type, status, limit, offset, sortAscending, cancellationToken);

    public Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, CancellationToken cancellationToken = default)
        => _pendingActionRepository.CountAsync(spokeId, projectId, type, status, cancellationToken);

    public async Task<PendingAction> ResolveAsync(Guid actionId, string action, string? notes = null, JsonDocument? modifications = null, CancellationToken cancellationToken = default)
    {
        if (!ValidActions.Contains(action))
            throw new Domain.Exceptions.ValidationException($"Invalid action '{action}'. Must be one of: approve, reject, respond");

        var pendingAction = await _pendingActionRepository.GetByIdAsync(actionId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"PendingAction {actionId} not found");

        if (pendingAction.Status != PendingActionStatus.Pending)
            throw new Domain.Exceptions.ConflictException($"PendingAction {actionId} is already resolved (status: {pendingAction.Status})");

        pendingAction.Status = action.ToLowerInvariant() switch
        {
            "approve" => PendingActionStatus.Approved,
            "reject" => PendingActionStatus.Rejected,
            _ => PendingActionStatus.Resolved // "respond" — already validated
        };
        pendingAction.ResolvedAt = DateTimeOffset.UtcNow;

        // Merge notes and modifications into metadata
        if (notes is not null || modifications is not null)
        {
            var metadataNode = pendingAction.Metadata is not null
                ? JsonNode.Parse(pendingAction.Metadata.RootElement.GetRawText()) as JsonObject ?? new JsonObject()
                : new JsonObject();

            if (notes is not null)
                metadataNode["notes"] = notes;
            if (modifications is not null)
                metadataNode["modifications"] = JsonNode.Parse(modifications.RootElement.GetRawText());

            pendingAction.Metadata = JsonDocument.Parse(metadataNode.ToJsonString());
        }

        await _pendingActionRepository.UpdateAsync(pendingAction, cancellationToken);
        _logger.LogInformation("PendingAction {ActionId} resolved with action '{Action}'", actionId, action);
        return pendingAction;
    }
}
