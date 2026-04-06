using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class ConversationsControllerTests
{
    private readonly Mock<IConversationService> _conversationServiceMock = new();
    private readonly Mock<IHubContext<NexusHub>> _hubContextMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _dashboardProxyMock = new();
    private readonly Mock<IClientProxy> _spokeProxyMock = new();
    private readonly Mock<ILogger<ConversationsController>> _loggerMock = new();
    private readonly ConversationsController _controller;

    public ConversationsControllerTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group("dashboard")).Returns(_dashboardProxyMock.Object);
        _clientsMock.Setup(c => c.Group(It.Is<string>(s => s.StartsWith("spoke-")))).Returns(_spokeProxyMock.Object);

        _controller = new ConversationsController(
            _conversationServiceMock.Object,
            _hubContextMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task ListAsync_ReturnsConversations()
    {
        var convId = Guid.NewGuid();
        var conversations = new List<Conversation>
        {
            new() { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };

        _conversationServiceMock.Setup(s => s.ListConversationsAsync(null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversations);
        _conversationServiceMock.Setup(s => s.GetConversationCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _controller.ListAsync(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ConversationListResponse>(ok.Value);
        Assert.Single(response.Conversations);
        Assert.Equal(1, response.Total);
        Assert.Equal(0, response.Conversations[0].MessageCount);
    }

    [Fact]
    public async Task ListAsync_FiltersBySpokeId()
    {
        var spokeId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.ListConversationsAsync(spokeId, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());
        _conversationServiceMock.Setup(s => s.GetConversationCountAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(spokeId: spokeId, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        _conversationServiceMock.Verify(s => s.ListConversationsAsync(spokeId, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_ClampsLimit()
    {
        _conversationServiceMock.Setup(s => s.ListConversationsAsync(null, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());
        _conversationServiceMock.Setup(s => s.GetConversationCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(limit: 999, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ConversationListResponse>(ok.Value);
        Assert.Equal(100, response.Limit);
    }

    [Fact]
    public async Task ListAsync_NegativeOffset_Returns400()
    {
        var result = await _controller.ListAsync(offset: -1, cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task GetAsync_ExistingConversation_ReturnsDetailWithMessages()
    {
        var convId = Guid.NewGuid();
        var msgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _conversationServiceMock.Setup(s => s.GetConversationWithMessagesAsync(convId, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation
            {
                Id = convId,
                Title = "Test Conv",
                CreatedAt = now,
                UpdatedAt = now,
                Messages = new List<ConversationMessage>
                {
                    new() { Id = msgId, ConversationId = convId, Role = ConversationRole.User, Content = "hello", Timestamp = now }
                }
            });
        _conversationServiceMock.Setup(s => s.GetMessageCountAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _controller.GetAsync(convId, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ConversationDetailResponse>(ok.Value);
        Assert.Equal(convId, response.Id);
        Assert.Single(response.Messages);
        Assert.Equal("user", response.Messages[0].Role);
    }

    [Fact]
    public async Task GetAsync_NonExistent_Returns404()
    {
        var convId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.GetConversationWithMessagesAsync(convId, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await _controller.GetAsync(convId, cancellationToken: CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("RESOURCE_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_Returns201()
    {
        var convId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _conversationServiceMock.Setup(s => s.CreateConversationAsync(null, "My Title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation { Id = convId, Title = "My Title", CreatedAt = now, UpdatedAt = now });

        var request = new CreateConversationRequest { Title = "My Title" };
        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal($"/api/conversations/{convId}", created.Location);
        var response = Assert.IsType<ConversationSummaryResponse>(created.Value);
        Assert.Equal("My Title", response.Title);
    }

    [Fact]
    public async Task CreateAsync_EmptyTitle_Returns400()
    {
        var request = new CreateConversationRequest { Title = " " };
        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
        Assert.Contains("Title", error.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_InvalidSpokeId_Returns404()
    {
        var spokeId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.CreateConversationAsync(spokeId, "Test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Spoke with id '{spokeId}' not found"));

        var request = new CreateConversationRequest { SpokeId = spokeId, Title = "Test" };
        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("RESOURCE_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_ExistingConversation_Returns204()
    {
        var convId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.ArchiveConversationAsync(convId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ArchiveAsync(convId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ArchiveAsync_NonExistent_Returns404()
    {
        var convId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.ArchiveConversationAsync(convId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("not found"));

        var result = await _controller.ArchiveAsync(convId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("RESOURCE_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task SendMessageAsync_ValidMessage_Returns201AndBroadcasts()
    {
        var convId = Guid.NewGuid();
        var msgId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _conversationServiceMock.Setup(s => s.GetConversationAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation { Id = convId, SpokeId = spokeId, Title = "Test", CreatedAt = now, UpdatedAt = now });
        _conversationServiceMock.Setup(s => s.AddMessageAsync(convId, ConversationRole.User, "hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationMessage { Id = msgId, ConversationId = convId, Role = ConversationRole.User, Content = "hello", Timestamp = now });

        var request = new SendConversationMessageRequest { Content = "hello" };
        var result = await _controller.SendMessageAsync(convId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ConversationMessageResponse>(created.Value);
        Assert.Equal("user", response.Role);
        Assert.Equal("hello", response.Content);

        // Verify SignalR broadcast to dashboard
        _dashboardProxyMock.Verify(
            c => c.SendCoreAsync("ConversationMessageReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify dispatch to spoke
        _spokeProxyMock.Verify(
            c => c.SendCoreAsync("SendConversationMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_EmptyContent_Returns400()
    {
        var convId = Guid.NewGuid();
        var request = new SendConversationMessageRequest { Content = "" };
        var result = await _controller.SendMessageAsync(convId, request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task SendMessageAsync_NonExistentConversation_Returns404()
    {
        var convId = Guid.NewGuid();
        _conversationServiceMock.Setup(s => s.GetConversationAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var request = new SendConversationMessageRequest { Content = "hello" };
        var result = await _controller.SendMessageAsync(convId, request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("RESOURCE_NOT_FOUND", error.Error.Code);
    }
}
