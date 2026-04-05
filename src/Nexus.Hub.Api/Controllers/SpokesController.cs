using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nexus.Hub.Api.Models;
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

        var expectedPsk = _configuration["Spoke:PreSharedKey"];
        if (string.IsNullOrEmpty(expectedPsk) || request.Psk != expectedPsk)
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
}
