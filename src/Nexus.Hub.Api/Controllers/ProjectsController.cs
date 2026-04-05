using Microsoft.AspNetCore.Mvc;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(
    IProjectService projectService,
    ISpokeService spokeService,
    IJobService jobService,
    ILogger<ProjectsController> logger) : ControllerBase
{
    private readonly IProjectService _projectService = projectService;
    private readonly ISpokeService _spokeService = spokeService;
    private readonly IJobService _jobService = jobService;
    private readonly ILogger<ProjectsController> _logger = logger;

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Project name is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        if (request.SpokeId == Guid.Empty)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "SpokeId is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var project = await _projectService.CreateProjectAsync(request.SpokeId, request.Name, request.ExternalKey, request.Summary, cancellationToken);

        var response = new ProjectResponse
        {
            Id = project.Id,
            SpokeId = project.SpokeId,
            ExternalKey = project.ExternalKey,
            Name = project.Name,
            Status = project.Status,
            Summary = project.Summary,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            SpokeName = string.Empty,
            ActiveJobCount = 0,
            TotalJobCount = 0
        };

        return Created($"/api/projects/{project.Id}", response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetProjectAsync(id, cancellationToken);

        var spoke = await _spokeService.GetSpokeAsync(project.SpokeId, cancellationToken);
        var activeJobCount = await _jobService.GetJobCountAsync(projectId: id, status: JobStatus.Running, cancellationToken: cancellationToken);
        var totalJobCount = await _jobService.GetJobCountAsync(projectId: id, cancellationToken: cancellationToken);

        var response = new ProjectResponse
        {
            Id = project.Id,
            SpokeId = project.SpokeId,
            ExternalKey = project.ExternalKey,
            Name = project.Name,
            Status = project.Status,
            Summary = project.Summary,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            SpokeName = spoke!.Name,
            ActiveJobCount = activeJobCount,
            TotalJobCount = totalJobCount
        };

        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStatusAsync(Guid id, [FromBody] UpdateProjectStatusRequest request, CancellationToken cancellationToken)
    {
        await _projectService.UpdateProjectStatusAsync(id, request.Status, cancellationToken);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _projectService.UpdateProjectStatusAsync(id, ProjectStatus.Archived, cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/jobs")]
    public async Task<IActionResult> ListJobsByProjectAsync(
        Guid projectId,
        [FromQuery] JobStatus? status = null,
        [FromQuery] JobType? type = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
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

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "'from' must be before or equal to 'to'",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        await _projectService.GetProjectAsync(projectId, cancellationToken);

        var jobs = await _jobService.ListJobsAsync(projectId: projectId, status: status, type: type, from: from, to: to, limit: limit, offset: offset, cancellationToken: cancellationToken);
        var total = await _jobService.GetJobCountAsync(projectId: projectId, status: status, type: type, from: from, to: to, cancellationToken: cancellationToken);

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
}
