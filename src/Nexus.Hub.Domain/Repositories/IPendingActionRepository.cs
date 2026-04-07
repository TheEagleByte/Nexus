using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IPendingActionRepository
{
    Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, int limit = 50, int offset = 0, bool sortAscending = true, CancellationToken cancellationToken = default);
    Task<PendingAction> AddAsync(PendingAction action, CancellationToken cancellationToken = default);
    Task UpdateAsync(PendingAction action, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, Guid? projectId = null, PendingActionType? type = null, PendingActionStatus? status = null, CancellationToken cancellationToken = default);
}
