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

    public async Task<Job> CreateJobAsync(Guid spokeId, Guid projectId, JobType type, bool requiresApproval = false, JsonDocument? context = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = type,
            Status = requiresApproval ? JobStatus.AwaitingApproval : JobStatus.Queued,
            ApprovalRequired = requiresApproval,
            Summary = context?.RootElement.ToString(),
            CreatedAt = now
        };

        await _jobRepository.AddAsync(job, cancellationToken);
        _logger.LogInformation("Job {JobId} created for spoke {SpokeId} on project {ProjectId} (status: {Status})",
            job.Id, spokeId, projectId, job.Status);
        return job;
    }

    public async Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Job not found: {JobId}", jobId);
            throw new Domain.Exceptions.NotFoundException($"Job {jobId} not found");
        }
        return job;
    }

    public Task<List<Job>> ListJobsAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, JobType? type = null, DateTimeOffset? from = null, DateTimeOffset? to = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _jobRepository.ListAsync(spokeId, projectId, status, type, from, to, limit, offset, cancellationToken);

    public async Task ApproveJobAsync(Guid jobId, bool approved = true, string? approvedBy = null, JsonDocument? modifications = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Job {jobId} not found");

        if (job.Status != JobStatus.AwaitingApproval)
            throw new Domain.Exceptions.ValidationException($"Job {jobId} is not awaiting approval (current status: {job.Status})");

        var now = DateTimeOffset.UtcNow;

        if (approved)
        {
            job.Status = JobStatus.Queued;
            job.ApprovedAt = now;
            job.ApprovedBy = approvedBy;
            _logger.LogInformation("Job {JobId} approved by {ApprovedBy}", jobId, approvedBy ?? "unknown");
        }
        else
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = now;
            job.Summary = "Rejected" + (modifications is not null ? $": {modifications.RootElement}" : "");
            _logger.LogInformation("Job {JobId} rejected by {ApprovedBy}", jobId, approvedBy ?? "unknown");
        }

        await _jobRepository.UpdateAsync(job, cancellationToken);
    }

    public async Task CancelJobAsync(Guid jobId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Job {jobId} not found");

        if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
            throw new Domain.Exceptions.ValidationException($"Job {jobId} is already in terminal state: {job.Status}");

        job.Status = JobStatus.Cancelled;
        job.CompletedAt = DateTimeOffset.UtcNow;
        if (reason is not null) job.Summary = reason;

        await _jobRepository.UpdateAsync(job, cancellationToken);
        _logger.LogInformation("Job {JobId} cancelled. Reason: {Reason}", jobId, reason ?? "none");
    }

    public async Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? summary = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Job {jobId} not found");

        var now = DateTimeOffset.UtcNow;
        job.Status = status;

        if (summary is not null) job.Summary = summary;
        if (status == JobStatus.Running && job.StartedAt is null) job.StartedAt = now;
        if (status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled && job.CompletedAt is null) job.CompletedAt = now;

        await _jobRepository.UpdateAsync(job, cancellationToken);
        _logger.LogInformation("Job {JobId} status updated to {Status}", jobId, status);
    }

    public async Task<OutputStream> RecordJobOutputAsync(Guid jobId, string content, string streamType = "stdout", CancellationToken cancellationToken = default)
    {
        return await _outputStreamRepository.AddWithAutoSequenceAsync(jobId, content, streamType, cancellationToken);
    }

    public Task<List<OutputStream>> GetJobOutputAsync(Guid jobId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        => _outputStreamRepository.ListByJobAsync(jobId, limit, offset, cancellationToken);

    public Task<int> GetJobOutputCountAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _outputStreamRepository.CountByJobAsync(jobId, cancellationToken);

    public Task<int> GetJobCountAsync(Guid? spokeId = null, Guid? projectId = null, JobStatus? status = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
        => _jobRepository.CountAsync(spokeId, projectId, status, from, to, cancellationToken);
}
