using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/spokes")]
public class SpokesController(ISpokeService spokeService, IConfiguration configuration) : ControllerBase
{
    private readonly ISpokeService _spokeService = spokeService;
    private readonly IConfiguration _configuration = configuration;

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

        var spoke = await _spokeService.RegisterSpokeAsync(request.Name, capabilities, config, profile, cancellationToken);

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
}
