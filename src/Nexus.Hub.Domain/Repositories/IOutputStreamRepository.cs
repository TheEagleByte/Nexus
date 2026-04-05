using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IOutputStreamRepository
{
    Task<List<OutputStream>> ListByJobAsync(Guid jobId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<OutputStream> AddAsync(OutputStream outputStream, CancellationToken cancellationToken = default);
    Task<int> CountByJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<long> GetNextSequenceAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<OutputStream> AddWithAutoSequenceAsync(Guid jobId, string content, CancellationToken cancellationToken = default);
}
