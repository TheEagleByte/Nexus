using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController(
    IJobService jobService,
    IProjectService projectService,
    IHubContext<NexusHub> hubContext,
    ILogger<JobsController> logger) : ControllerBase
{
    private readonly IJobService _jobService = jobService;
    private readonly IProjectService _projectService = projectService;
    private readonly IHubContext<NexusHub> _hubContext = hubContext;
    private readonly ILogger<JobsController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] JobStatus? status = null,
        [FromQuery] JobType? type = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Offset must be non-negative",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        limit = Math.Clamp(limit, 1, 100);

        var jobs = await _jobService.ListJobsAsync(status: status, type: type, limit: limit, offset: offset, cancellationToken: cancellationToken);
        var total = await _jobService.GetJobCountAsync(status: status, type: type, cancellationToken: cancellationToken);

        var response = new JobListResponse
        {
            Jobs = jobs.Select(j => new JobResponse
            {
                Id = j.Id,
                ProjectId = j.ProjectId,
                SpokeId = j.SpokeId,
                Type = j.Type,
                Status = j.Status,
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                Summary = j.Summary
            }).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "ProjectId is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var project = await _projectService.GetProjectAsync(request.ProjectId, cancellationToken);

        var job = await _jobService.CreateJobAsync(project!.SpokeId, project.Id, request.Type, request.RequiresApproval, request.Context, cancellationToken);

        var response = new JobResponse
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            SpokeId = job.SpokeId,
            Type = job.Type,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            Summary = job.Summary
        };

        return Created($"/api/jobs/{job.Id}", response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        var outputCount = await _jobService.GetJobOutputCountAsync(id, cancellationToken);
        var outputTotalBytes = await _jobService.GetJobOutputTotalBytesAsync(id, cancellationToken);

        var response = new JobDetailResponse
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            SpokeId = job.SpokeId,
            Type = job.Type,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            Summary = job.Summary,
            OutputChunkCount = outputCount,
            OutputTotalBytes = outputTotalBytes
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsync(Guid id, [FromBody] ApproveJobRequest request, CancellationToken cancellationToken)
    {
        await _jobService.ApproveJobAsync(id, request.Approved, approvedBy: null, request.Modifications, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelAsync(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelJobRequest? request = null, CancellationToken cancellationToken = default)
    {
        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        var spokeId = job.SpokeId;

        await _jobService.CancelJobAsync(id, request?.Reason, cancellationToken);

        try
        {
            await _hubContext.Clients.Group($"spoke-{spokeId}").SendAsync("JobCancelled", new { JobId = id, Reason = request?.Reason }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify spoke {SpokeId} of job {JobId} cancellation", spokeId, id);
        }

        return Accepted();
    }

    [HttpPost("{id:guid}/output")]
    public async Task<IActionResult> RecordOutputAsync(Guid id, [FromBody] RecordOutputRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Output content is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        await _jobService.RecordJobOutputAsync(id, request.Content, request.StreamType, cancellationToken);
        return Accepted();
    }

    [HttpGet("{id:guid}/output")]
    public async Task<IActionResult> GetOutputAsync(
        Guid id,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Offset must be non-negative",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        if (limit < 1)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Limit must be at least 1",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        limit = Math.Min(limit, 100);

        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        var chunks = await _jobService.GetJobOutputAsync(id, limit, offset, cancellationToken);
        var totalChunks = await _jobService.GetJobOutputCountAsync(id, cancellationToken);

        var isComplete = job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;

        var response = new JobOutputResponse
        {
            JobId = id,
            Chunks = chunks.Select(c => new OutputChunk
            {
                Sequence = c.Sequence,
                Content = c.Content,
                StreamType = c.StreamType,
                Timestamp = c.Timestamp
            }).ToList(),
            TotalChunks = totalChunks,
            Limit = limit,
            Offset = offset,
            IsComplete = isComplete
        };

        return Ok(response);
    }
}
