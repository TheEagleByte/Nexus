using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController(
    IMessageService messageService,
    ISpokeService spokeService,
    IHubContext<NexusHub> hubContext,
    ILogger<MessagesController> logger) : ControllerBase
{
    private readonly IMessageService _messageService = messageService;
    private readonly ISpokeService _spokeService = spokeService;
    private readonly IHubContext<NexusHub> _hubContext = hubContext;
    private readonly ILogger<MessagesController> _logger = logger;

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
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

        await _spokeService.GetSpokeAsync(request.SpokeId, cancellationToken);

        var message = await _messageService.RecordMessageAsync(request.SpokeId, request.Direction, request.Content, request.JobId, cancellationToken);

        await _hubContext.Clients.All.SendAsync("MessageReceived", new
        {
            message.Id,
            message.SpokeId,
            Direction = message.Direction.ToString(),
            message.Content,
            message.JobId,
            message.Timestamp
        }, cancellationToken);

        var response = new MessageResponse
        {
            Id = message.Id,
            SpokeId = message.SpokeId,
            Direction = message.Direction,
            Content = message.Content,
            JobId = message.JobId,
            Timestamp = message.Timestamp
        };

        return Created($"/api/messages/{message.Id}", response);
    }
}
