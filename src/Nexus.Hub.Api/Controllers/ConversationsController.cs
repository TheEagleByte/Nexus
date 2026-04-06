using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController(
    IConversationService conversationService,
    ISpokeService spokeService,
    IHubContext<NexusHub> hubContext,
    ILogger<ConversationsController> logger) : ControllerBase
{
    private readonly IConversationService _conversationService = conversationService;
    private readonly ISpokeService _spokeService = spokeService;
    private readonly IHubContext<NexusHub> _hubContext = hubContext;
    private readonly ILogger<ConversationsController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? spokeId = null,
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

        var conversations = await _conversationService.ListConversationsAsync(spokeId, limit, offset, cancellationToken);
        var total = await _conversationService.GetConversationCountAsync(spokeId, cancellationToken);

        var items = new List<ConversationSummaryResponse>();
        foreach (var c in conversations)
        {
            var messageCount = await _conversationService.GetMessageCountAsync(c.Id, cancellationToken);
            items.Add(MapToSummary(c, messageCount));
        }

        return Ok(new ConversationListResponse
        {
            Conversations = items,
            Total = total,
            Limit = limit,
            Offset = offset
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(
        Guid id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var conversation = await _conversationService.GetConversationWithMessagesAsync(id, limit, offset, cancellationToken);
        if (conversation is null)
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = $"Conversation with id '{id}' not found",
                    Status = 404,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var messageCount = await _conversationService.GetMessageCountAsync(id, cancellationToken);

        var response = new ConversationDetailResponse
        {
            Id = conversation.Id,
            SpokeId = conversation.SpokeId,
            SpokeName = conversation.Spoke?.Name,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            CcSessionId = conversation.CcSessionId,
            MessageCount = messageCount,
            Messages = conversation.Messages.Select(m => new ConversationMessageResponse
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content,
                Timestamp = m.Timestamp
            }).ToList()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "INVALID_REQUEST",
                    Message = "Title is required",
                    Status = 400,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        try
        {
            var conversation = await _conversationService.CreateConversationAsync(request.SpokeId, request.Title, cancellationToken);
            var response = MapToSummary(conversation, 0);
            return Created($"/api/conversations/{conversation.Id}", response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = ex.Message,
                    Status = 404,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _conversationService.ArchiveConversationAsync(id, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = $"Conversation with id '{id}' not found",
                    Status = 404,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });
        }
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendMessageAsync(Guid id, [FromBody] SendConversationMessageRequest request, CancellationToken cancellationToken)
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

        Conversation? conversation;
        try
        {
            conversation = await _conversationService.GetConversationAsync(id, cancellationToken);
        }
        catch (Exception)
        {
            conversation = null;
        }

        if (conversation is null)
            return NotFound(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Code = "RESOURCE_NOT_FOUND",
                    Message = $"Conversation with id '{id}' not found",
                    Status = 404,
                    CorrelationId = HttpContext.TraceIdentifier
                }
            });

        var message = await _conversationService.AddMessageAsync(id, ConversationRole.User, request.Content, cancellationToken);

        // Broadcast to dashboard clients
        await _hubContext.Clients.Group("dashboard").SendAsync("ConversationMessageReceived",
            new ConversationMessageReceivedEvent(
                id, message.Id, "user", message.Content, message.Timestamp, false),
            cancellationToken);

        // Dispatch to spoke if conversation is spoke-bound
        if (conversation.SpokeId.HasValue)
        {
            await _hubContext.Clients.Group($"spoke-{conversation.SpokeId}")
                .SendAsync("SendConversationMessage",
                    new ConversationUserMessage(id, conversation.SpokeId.Value, request.Content, DateTimeOffset.UtcNow),
                    cancellationToken);
        }

        var response = new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            Role = message.Role.ToString().ToLowerInvariant(),
            Content = message.Content,
            Timestamp = message.Timestamp
        };

        return Created($"/api/conversations/{id}/messages/{message.Id}", response);
    }

    private static ConversationSummaryResponse MapToSummary(Conversation c, int messageCount) => new()
    {
        Id = c.Id,
        SpokeId = c.SpokeId,
        SpokeName = c.Spoke?.Name,
        Title = c.Title,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        CcSessionId = c.CcSessionId,
        MessageCount = messageCount
    };
}
