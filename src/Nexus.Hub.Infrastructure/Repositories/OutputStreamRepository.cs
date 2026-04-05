using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class OutputStreamRepository(NexusDbContext context) : IOutputStreamRepository
{
    private readonly NexusDbContext _context = context;

    public Task<List<OutputStream>> ListByJobAsync(Guid jobId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<OutputStream> AddAsync(OutputStream outputStream, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CountByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<long> GetNextSequenceAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
