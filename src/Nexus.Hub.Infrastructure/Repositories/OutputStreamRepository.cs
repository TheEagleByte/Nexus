using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class OutputStreamRepository(NexusDbContext context) : IOutputStreamRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<List<OutputStream>> ListByJobAsync(Guid jobId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        => await _context.OutputStreams
            .Where(o => o.JobId == jobId)
            .OrderBy(o => o.Sequence)
            .ThenBy(o => o.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<OutputStream> AddAsync(OutputStream outputStream, CancellationToken cancellationToken = default)
    {
        _context.OutputStreams.Add(outputStream);
        await _context.SaveChangesAsync(cancellationToken);
        return outputStream;
    }

    public async Task<int> CountByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await _context.OutputStreams.Where(o => o.JobId == jobId).CountAsync(cancellationToken);

    public async Task<long> GetNextSequenceAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var max = await _context.OutputStreams
            .Where(o => o.JobId == jobId)
            .MaxAsync(o => (long?)o.Sequence, cancellationToken);
        return (max ?? -1) + 1;
    }

    public async Task<OutputStream> AddWithAutoSequenceAsync(Guid jobId, string content, string streamType = "stdout", CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var max = await _context.OutputStreams
                .Where(o => o.JobId == jobId)
                .MaxAsync(o => (long?)o.Sequence, cancellationToken);

            var outputStream = new OutputStream
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Sequence = (max ?? -1) + 1,
                Content = content,
                StreamType = streamType,
                Timestamp = DateTimeOffset.UtcNow
            };

            _context.OutputStreams.Add(outputStream);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return outputStream;
        });
    }

    public async Task<long> TotalBytesByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await _context.OutputStreams
            .Where(o => o.JobId == jobId)
            .SumAsync(o => (long)o.Content.Length, cancellationToken);
}
