using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/spokes")]
public class SpokesController(
    ISpokeService spokeService,
    IProjectService projectService,
    IMessageService messageService,
    IHubContext<NexusHub> hubContext,
    IConfiguration configuration,
    ILogger<SpokesController> logger) : ControllerBase
{
    private readonly ISpokeService _spokeService = spokeService;
    private readonly IProjectService _projectService = projectService;
    private readonly IMessageService _messageService = messageService;
    private readonly IHubContext<NexusHub> _hubContext = hubContext;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<SpokesController> _logger = logger;

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] SpokeRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Spoke name is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var expectedPsk = _configuration["Spoke:PreSharedKey"] ?? string.Empty;
        var providedPsk = request.Psk ?? string.Empty;
        var expectedBytes = Encoding.UTF8.GetBytes(expectedPsk);
        var providedBytes = Encoding.UTF8.GetBytes(providedPsk);
        var isPskValid =
            expectedBytes.Length > 0 &&
            expectedBytes.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

        if (!isPskValid)
            return Unauthorized(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "UNAUTHORIZED",
                    Message = "Invalid pre-shared key",
                    Status = 401,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var capabilities = JsonSerializer.SerializeToDocument(request.Capabilities);
        var config = request.Config ?? JsonSerializer.SerializeToDocument(new { });

        JsonDocument? profile = null;
        if (request.Os is not null || request.Architecture is not null || request.Profile is not null || request.Metadata is not null)
        {
            profile = JsonSerializer.SerializeToDocument(new
            {
                os = request.Os,
                architecture = request.Architecture,
                custom = request.Profile,
                metadata = request.Metadata
            });
        }

        var spoke = await _spokeService.RegisterSpokeAsync(request.Name, capabilities, config, profile, cancellationToken: cancellationToken);

        var response = new SpokeDetailResponse
        {
            Id = spoke.Id,
            Name = spoke.Name,
            Status = spoke.Status,
            LastSeen = spoke.LastSeen,
            Capabilities = spoke.Capabilities,
            Config = spoke.Config,
            Profile = spoke.Profile,
            RegisteredAt = spoke.CreatedAt
        };

        return Created($"/api/spokes/{spoke.Id}", response);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] SpokeStatus? status,
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

        var spokes = await _spokeService.ListSpokesAsync(status, limit, offset, cancellationToken);
        var total = await _spokeService.GetSpokeCountAsync(status, cancellationToken);

        var response = new SpokeListResponse
        {
            Spokes = spokes.Select(s => new SpokeResponse
            {
                Id = s.Id,
                Name = s.Name,
                Status = s.Status,
                LastSeen = s.LastSeen,
                Capabilities = s.Capabilities,
                Config = s.Config
            }).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var spoke = await _spokeService.GetSpokeAsync(id, cancellationToken);

        var response = new SpokeDetailResponse
        {
            Id = spoke!.Id,
            Name = spoke.Name,
            Status = spoke.Status,
            LastSeen = spoke.LastSeen,
            Capabilities = spoke.Capabilities,
            Config = spoke.Config,
            Profile = spoke.Profile,
            RegisteredAt = spoke.CreatedAt
        };

        return Ok(response);
    }

    [HttpGet("{spokeId:guid}/projects")]
    public async Task<IActionResult> ListProjectsAsync(
        Guid spokeId,
        [FromQuery] ProjectStatus? status = null,
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

        var spoke = await _spokeService.GetSpokeAsync(spokeId, cancellationToken);

        var projects = await _projectService.ListProjectsAsync(spokeId, status, limit, offset, cancellationToken);
        var total = await _projectService.GetProjectCountAsync(spokeId, status, cancellationToken);

        var response = new ProjectListResponse
        {
            Projects = projects.Select(p => new ProjectResponse
            {
                Id = p.Id,
                SpokeId = p.SpokeId,
                ExternalKey = p.ExternalKey,
                Name = p.Name,
                Status = p.Status,
                Summary = p.Summary,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                SpokeName = spoke!.Name,
                ActiveJobCount = 0,
                TotalJobCount = 0
            }).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}/conversation")]
    public async Task<IActionResult> GetConversationAsync(
        Guid id,
        [FromQuery] Guid? jobId = null,
        [FromQuery] MessageDirection? direction = null,
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

        // Verify spoke exists
        await _spokeService.GetSpokeAsync(id, cancellationToken);

        var messages = await _messageService.GetConversationAsync(id, jobId, direction, limit, offset, cancellationToken);
        var total = await _messageService.GetMessageCountAsync(id, jobId, direction, cancellationToken);

        var response = new ConversationResponse
        {
            Messages = messages.Select(m => new MessageResponse
            {
                Id = m.Id,
                SpokeId = m.SpokeId,
                Direction = m.Direction,
                Content = m.Content,
                JobId = m.JobId,
                Timestamp = m.Timestamp
            }).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/conversation")]
    public async Task<IActionResult> SendMessageAsync(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Message content is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        // Verify spoke exists
        await _spokeService.GetSpokeAsync(id, cancellationToken);

        try
        {
            await NexusHub.DispatchMessageToSpoke(_hubContext, _messageService, _logger, id, request.Content, request.JobId);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "SPOKE_NOT_CONNECTED",
                    Message = $"Spoke {id} is not currently connected",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });
        }

        return Accepted();
    }
}
