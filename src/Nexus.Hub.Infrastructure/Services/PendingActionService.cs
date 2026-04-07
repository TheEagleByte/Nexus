using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class PendingActionService(IPendingActionRepository pendingActionRepository, ILogger<PendingActionService> logger) : IPendingActionService
{
    private readonly IPendingActionRepository _pendingActionRepository = pendingActionRepository;
    private readonly ILogger<PendingActionService> _logger = logger;

    public async Task<PendingAction> CreateAsync(Guid spokeId, Guid projectId, Guid jobId, PendingActionType type, int priority, JsonDocument? metadata = null, CancellationToken cancellationToken = default)
    {
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
        _logger.LogInformation("PendingAction {ActionId} created for spoke {SpokeId}, job {JobId} (type: {Type}, priority: {Priority})",
            action.Id, spokeId, jobId, type, priority);
        return action;
    }

    public async Task ResolveAsync(Guid actionId, bool approved, string? resolution = null, CancellationToken cancellationToken = default)
    {
        var action = await _pendingActionRepository.GetByIdAsync(actionId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"PendingAction {actionId} not found");

        if (action.Status != PendingActionStatus.Pending)
            throw new Domain.Exceptions.ValidationException($"PendingAction {actionId} is not pending (current status: {action.Status})");

        action.Status = approved ? PendingActionStatus.Approved : PendingActionStatus.Rejected;
        action.ResolvedAt = DateTimeOffset.UtcNow;

        if (resolution is not null)
        {
            action.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { resolution }));
        }

        await _pendingActionRepository.UpdateAsync(action, cancellationToken);
        _logger.LogInformation("PendingAction {ActionId} resolved as {Status} by user", actionId, action.Status);
    }

    public async Task<PendingAction> GetAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var action = await _pendingActionRepository.GetByIdAsync(actionId, cancellationToken);
        if (action is null)
        {
            _logger.LogWarning("PendingAction not found: {ActionId}", actionId);
            throw new Domain.Exceptions.NotFoundException($"PendingAction {actionId} not found");
        }
        return action;
    }

    public Task<List<PendingAction>> ListBySpokeAsync(Guid spokeId, PendingActionStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _pendingActionRepository.ListBySpokeAsync(spokeId, status, limit, offset, cancellationToken);

    public Task<List<PendingAction>> ListAllAsync(PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _pendingActionRepository.ListAsync(null, status, type, minPriority, limit, offset, cancellationToken);

    public Task<int> CountAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, CancellationToken cancellationToken = default)
        => _pendingActionRepository.CountAsync(spokeId, status, type, minPriority, cancellationToken);
}
