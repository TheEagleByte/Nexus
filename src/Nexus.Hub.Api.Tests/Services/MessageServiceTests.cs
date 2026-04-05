using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class MessageServiceTests
{
    private readonly Mock<IMessageRepository> _repo = new();
    private readonly Mock<ILogger<MessageService>> _logger = new();
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _sut = new MessageService(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task RecordMessageAsync_CreatesAndPersistsMessage()
    {
        var spokeId = Guid.NewGuid();
        _repo
            .Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message m, CancellationToken _) => m);

        var result = await _sut.RecordMessageAsync(spokeId, MessageDirection.UserToSpoke, "hello");

        Assert.Equal(spokeId, result.SpokeId);
        Assert.Equal(MessageDirection.UserToSpoke, result.Direction);
        Assert.Equal("hello", result.Content);
        Assert.NotEqual(Guid.Empty, result.Id);
        _repo.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordMessageAsync_SetsJobIdWhenProvided()
    {
        var jobId = Guid.NewGuid();
        _repo
            .Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message m, CancellationToken _) => m);

        var result = await _sut.RecordMessageAsync(Guid.NewGuid(), MessageDirection.SpokeToUser, "output", jobId);

        Assert.Equal(jobId, result.JobId);
    }

    [Fact]
    public async Task RecordMessageAsync_SetsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _repo
            .Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message m, CancellationToken _) => m);

        var result = await _sut.RecordMessageAsync(Guid.NewGuid(), MessageDirection.UserToSpoke, "test");

        Assert.True(result.Timestamp >= before);
        Assert.True(result.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetConversationAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), SpokeId = spokeId, Direction = MessageDirection.UserToSpoke, Content = "hi", Timestamp = DateTimeOffset.UtcNow }
        };

        _repo
            .Setup(r => r.ListBySpokeAsync(spokeId, 25, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var result = await _sut.GetConversationAsync(spokeId, 25, 10);

        Assert.Single(result);
        Assert.Equal("hi", result[0].Content);
        _repo.Verify(r => r.ListBySpokeAsync(spokeId, 25, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessageCountAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        _repo
            .Setup(r => r.CountBySpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _sut.GetMessageCountAsync(spokeId);

        Assert.Equal(42, result);
    }
}
