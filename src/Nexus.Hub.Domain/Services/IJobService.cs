using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IJobService
{
    Task<Job> CreateJobAsync(Guid projectId, JobType type, bool requiresApproval = false, JsonDocument? context = null, CancellationToken cancellationToken = default);
    Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<List<Job>> ListJobsAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task ApproveJobAsync(Guid jobId, bool approved = true, string? approvedBy = null, JsonDocument? modifications = null, CancellationToken cancellationToken = default);
    Task CancelJobAsync(Guid jobId, string? reason = null, CancellationToken cancellationToken = default);
    Task RecordJobOutputAsync(Guid jobId, string content, CancellationToken cancellationToken = default);
    Task<int> GetJobCountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, CancellationToken cancellationToken = default);
}
