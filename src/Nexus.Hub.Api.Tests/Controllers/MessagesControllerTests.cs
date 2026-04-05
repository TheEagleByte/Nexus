using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class MessagesControllerTests
{
    private readonly Mock<IMessageService> _messageServiceMock = new();
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<IHubContext<NexusHub>> _hubContextMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private readonly Mock<ILogger<MessagesController>> _loggerMock = new();
    private readonly MessagesController _controller;

    public MessagesControllerTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        _controller = new MessagesController(
            _messageServiceMock.Object,
            _spokeServiceMock.Object,
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
    public async Task CreateAsync_ValidRequest_Returns201WithMessageResponse()
    {
        var spokeId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId,
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = System.Text.Json.JsonSerializer.SerializeToDocument(Array.Empty<string>()),
                Config = System.Text.Json.JsonSerializer.SerializeToDocument(new { }),
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message
            {
                Id = messageId,
                SpokeId = spokeId,
                Direction = MessageDirection.UserToSpoke,
                Content = "hello",
                Timestamp = now
            });

        var request = new CreateMessageRequest
        {
            SpokeId = spokeId,
            Direction = MessageDirection.UserToSpoke,
            Content = "hello"
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal($"/api/messages/{messageId}", createdResult.Location);

        var response = Assert.IsType<MessageResponse>(createdResult.Value);
        Assert.Equal(messageId, response.Id);
        Assert.Equal(spokeId, response.SpokeId);
        Assert.Equal(MessageDirection.UserToSpoke, response.Direction);
        Assert.Equal("hello", response.Content);
        Assert.Equal(now, response.Timestamp);
    }

    [Fact]
    public async Task CreateAsync_EmptyContent_Returns400()
    {
        var request = new CreateMessageRequest
        {
            SpokeId = Guid.NewGuid(),
            Direction = MessageDirection.UserToSpoke,
            Content = ""
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
        Assert.Contains("content", error.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_EmptySpokeId_Returns400()
    {
        var request = new CreateMessageRequest
        {
            SpokeId = Guid.Empty,
            Direction = MessageDirection.UserToSpoke,
            Content = "hello"
        };

        var result = await _controller.CreateAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
        Assert.Contains("SpokeId", error.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_NonExistentSpoke_ThrowsNotFoundException()
    {
        var spokeId = Guid.NewGuid();

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Nexus.Hub.Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found"));

        var request = new CreateMessageRequest
        {
            SpokeId = spokeId,
            Direction = MessageDirection.UserToSpoke,
            Content = "hello"
        };

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _controller.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_BroadcastsViaSignalR()
    {
        var spokeId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId,
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = System.Text.Json.JsonSerializer.SerializeToDocument(Array.Empty<string>()),
                Config = System.Text.Json.JsonSerializer.SerializeToDocument(new { }),
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        _messageServiceMock
            .Setup(s => s.RecordMessageAsync(spokeId, MessageDirection.SpokeToUser, "response", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message
            {
                Id = messageId,
                SpokeId = spokeId,
                Direction = MessageDirection.SpokeToUser,
                Content = "response",
                Timestamp = now
            });

        var request = new CreateMessageRequest
        {
            SpokeId = spokeId,
            Direction = MessageDirection.SpokeToUser,
            Content = "response"
        };

        await _controller.CreateAsync(request, CancellationToken.None);

        _clientProxyMock.Verify(
            c => c.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
