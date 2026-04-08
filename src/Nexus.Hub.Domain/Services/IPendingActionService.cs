using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IPendingActionService
{
    Task<PendingAction> CreateAsync(Guid spokeId, Guid projectId, Guid jobId, PendingActionType type, int priority = 0, JsonDocument? metadata = null, CancellationToken cancellationToken = default);
    Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, int limit = 50, int offset = 0, bool sortAscending = true, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, CancellationToken cancellationToken = default);
    Task<PendingAction> ResolveAsync(Guid actionId, string action, string? notes = null, JsonDocument? modifications = null, CancellationToken cancellationToken = default);
}
