using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Handlers;

public class ConversationMessageHandlerTests
{
    private readonly Mock<IConversationRunner> _runnerMock;
    private readonly Mock<IHubConnectionService> _hubConnectionMock;
    private readonly ConversationMessageHandler _sut;

    public ConversationMessageHandlerTests()
    {
        _runnerMock = new Mock<IConversationRunner>();
        _hubConnectionMock = new Mock<IHubConnectionService>();

        _sut = new ConversationMessageHandler(
            _runnerMock.Object,
            _hubConnectionMock.Object,
            NullLogger<ConversationMessageHandler>.Instance);
    }

    [Fact]
    public void CommandType_IsConversationMessage()
    {
        Assert.Equal("conversation.message", _sut.CommandType);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_InvokesRunnerAndSendsResponse()
    {
        var convId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();
        var message = new ConversationUserMessage(convId, spokeId, "Hello CC", DateTimeOffset.UtcNow);
        var command = new CommandEnvelope("conversation.message", message, DateTimeOffset.UtcNow);

        _runnerMock
            .Setup(r => r.InvokeAsync(convId.ToString(), "Hello CC", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hello from CC");

        await _sut.HandleAsync(command, CancellationToken.None);

        _runnerMock.Verify(r => r.InvokeAsync(convId.ToString(), "Hello CC", null, It.IsAny<CancellationToken>()), Times.Once);
        _hubConnectionMock.Verify(h => h.SendAsync(
            "MessageFromSpokeConversation",
            It.Is<ConversationSpokeMessage>(m => m.ConversationId == convId && m.Content == "Hello from CC"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RunnerFailure_SendsErrorMessage()
    {
        var convId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();
        var message = new ConversationUserMessage(convId, spokeId, "Hello", DateTimeOffset.UtcNow);
        var command = new CommandEnvelope("conversation.message", message, DateTimeOffset.UtcNow);

        _runnerMock
            .Setup(r => r.InvokeAsync(convId.ToString(), "Hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CC CLI failed"));

        await _sut.HandleAsync(command, CancellationToken.None);

        _hubConnectionMock.Verify(h => h.SendAsync(
            "MessageFromSpokeConversation",
            It.Is<ConversationSpokeMessage>(m => m.ConversationId == convId && m.Content.Contains("[Error]")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_JsonElementPayload_DeserializesCorrectly()
    {
        var convId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        // Simulate what happens when the payload comes through the command queue as a JsonElement
        var jsonStr = JsonSerializer.Serialize(new ConversationUserMessage(convId, spokeId, "Test", DateTimeOffset.UtcNow));
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonStr);
        var command = new CommandEnvelope("conversation.message", jsonElement, DateTimeOffset.UtcNow);

        _runnerMock
            .Setup(r => r.InvokeAsync(It.IsAny<string>(), "Test", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        await _sut.HandleAsync(command, CancellationToken.None);

        _runnerMock.Verify(r => r.InvokeAsync(convId.ToString(), "Test", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
