using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class JobService(IJobRepository jobRepository, IOutputStreamRepository outputStreamRepository, ILogger<JobService> logger) : IJobService
{
    private readonly IJobRepository _jobRepository = jobRepository;
    private readonly IOutputStreamRepository _outputStreamRepository = outputStreamRepository;
    private readonly ILogger<JobService> _logger = logger;

    public Task<Job> CreateJobAsync(Guid projectId, JobType type, bool requiresApproval = false, JsonDocument? context = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Job>> ListJobsAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ApproveJobAsync(Guid jobId, bool approved = true, string? approvedBy = null, JsonDocument? modifications = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task CancelJobAsync(Guid jobId, string? reason = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task RecordJobOutputAsync(Guid jobId, string content, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetJobCountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
