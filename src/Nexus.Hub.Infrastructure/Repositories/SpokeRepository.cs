using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class SpokeRepository(NexusDbContext context) : ISpokeRepository
{
    private readonly NexusDbContext _context = context;

    public Task<Spoke?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Spoke>> ListAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Spoke> AddAsync(Spoke spoke, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(Spoke spoke, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
