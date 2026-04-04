using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Repositories;

public interface IOutputStreamRepository
{
    Task<List<OutputStream>> ListByJobAsync(Guid jobId, int limit = 100, int offset = 0);
    Task<OutputStream> AddAsync(OutputStream outputStream);
    Task<int> CountByJobAsync(Guid jobId);
    Task<long> GetNextSequenceAsync(Guid jobId);
}
