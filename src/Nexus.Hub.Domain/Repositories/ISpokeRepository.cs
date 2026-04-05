using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface ISpokeRepository
{
    Task<Spoke?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Spoke>> ListAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<Spoke> AddAsync(Spoke spoke, CancellationToken cancellationToken = default);
    Task UpdateAsync(Spoke spoke, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default);
}
