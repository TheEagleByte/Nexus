using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IPendingActionService
{
    Task<PendingAction> CreateAsync(Guid spokeId, Guid projectId, Guid jobId, PendingActionType type, int priority, JsonDocument? metadata = null, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid actionId, bool approved, string? resolution = null, CancellationToken cancellationToken = default);
    Task<PendingAction> GetAsync(Guid actionId, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListBySpokeAsync(Guid spokeId, PendingActionStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListAllAsync(PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, CancellationToken cancellationToken = default);
}
