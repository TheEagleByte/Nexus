using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class OutputStreamRepository(NexusDbContext context) : IOutputStreamRepository
{
    private readonly NexusDbContext _context = context;

    public Task<List<OutputStream>> ListByJobAsync(Guid jobId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public async Task<OutputStream> AddAsync(OutputStream outputStream, CancellationToken cancellationToken = default)
    {
        _context.OutputStreams.Add(outputStream);
        await _context.SaveChangesAsync(cancellationToken);
        return outputStream;
    }

    public Task<int> CountByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public async Task<long> GetNextSequenceAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var max = await _context.OutputStreams
            .Where(o => o.JobId == jobId)
            .MaxAsync(o => (long?)o.Sequence, cancellationToken);
        return (max ?? -1) + 1;
    }
}
