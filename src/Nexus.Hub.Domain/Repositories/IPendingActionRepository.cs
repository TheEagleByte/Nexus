using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IPendingActionRepository
{
    Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListBySpokeAsync(Guid spokeId, PendingActionStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<PendingAction>> ListAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<PendingAction> AddAsync(PendingAction entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(PendingAction entity, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid? spokeId = null, PendingActionStatus? status = null, PendingActionType? type = null, int? minPriority = null, CancellationToken cancellationToken = default);
}
