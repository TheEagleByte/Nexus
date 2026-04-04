using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface ISpokeRepository
{
    Task<Spoke?> GetByIdAsync(Guid id);
    Task<List<Spoke>> ListAsync(SpokeStatus? status = null, int limit = 50, int offset = 0);
    Task<Spoke> AddAsync(Spoke spoke);
    Task UpdateAsync(Spoke spoke);
    Task DeleteAsync(Guid id);
    Task<int> CountAsync(SpokeStatus? status = null);
}
