using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface IJobService
{
    Task<Job> CreateJobAsync(Guid projectId, JobType type, bool requiresApproval = false);
    Task<Job?> GetJobAsync(Guid jobId);
    Task<List<Job>> ListJobsAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0);
    Task ApproveJobAsync(Guid jobId, string? approvedBy = null);
    Task CancelJobAsync(Guid jobId);
    Task RecordJobOutputAsync(Guid jobId, string content);
    Task<int> GetJobCountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null);
}
