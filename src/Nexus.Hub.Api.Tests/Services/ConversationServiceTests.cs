using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class ConversationServiceTests
{
    private readonly Mock<IConversationRepository> _repo = new();
    private readonly Mock<ISpokeRepository> _spokeRepo = new();
    private readonly Mock<ILogger<ConversationService>> _logger = new();
    private readonly ConversationService _sut;

    public ConversationServiceTests()
    {
        _sut = new ConversationService(_repo.Object, _spokeRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateConversationAsync_SetsIdAndTimestamps()
    {
        _repo
            .Setup(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation c, CancellationToken _) => c);

        var before = DateTimeOffset.UtcNow;
        var result = await _sut.CreateConversationAsync(null, "Test Title");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Test Title", result.Title);
        Assert.False(result.IsArchived);
        Assert.True(result.CreatedAt >= before);
        Assert.True(result.UpdatedAt >= before);
        Assert.Null(result.SpokeId);
        _repo.Verify(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateConversationAsync_WithSpokeId_ValidatesSpoke()
    {
        var spokeId = Guid.NewGuid();
        _spokeRepo.Setup(r => r.GetByIdAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId, Name = "test", Status = SpokeStatus.Online,
                Capabilities = System.Text.Json.JsonSerializer.SerializeToDocument(Array.Empty<string>()),
                Config = System.Text.Json.JsonSerializer.SerializeToDocument(new { }),
                LastSeen = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            });
        _repo
            .Setup(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation c, CancellationToken _) => c);
        _repo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new Conversation { Id = id, SpokeId = spokeId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        var result = await _sut.CreateConversationAsync(spokeId, "Test");

        Assert.Equal(spokeId, result.SpokeId);
        _spokeRepo.Verify(r => r.GetByIdAsync(spokeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateConversationAsync_WithInvalidSpokeId_ThrowsNotFound()
    {
        var spokeId = Guid.NewGuid();
        _spokeRepo.Setup(r => r.GetByIdAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.CreateConversationAsync(spokeId, "Test"));
    }

    [Fact]
    public async Task ArchiveConversationAsync_SetsIsArchivedFlag()
    {
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        await _sut.ArchiveConversationAsync(convId);

        Assert.True(conversation.IsArchived);
        _repo.Verify(r => r.UpdateAsync(It.Is<Conversation>(c => c.IsArchived), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveConversationAsync_NonExistent_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.ArchiveConversationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddMessageAsync_CreatesMessageAndUpdatesConversation()
    {
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        _repo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        _repo.Setup(r => r.AddMessageAsync(It.IsAny<ConversationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationMessage m, CancellationToken _) => m);

        var before = DateTimeOffset.UtcNow;
        var result = await _sut.AddMessageAsync(convId, ConversationRole.User, "hello");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(convId, result.ConversationId);
        Assert.Equal(ConversationRole.User, result.Role);
        Assert.Equal("hello", result.Content);
        Assert.True(result.Timestamp >= before);
        // Conversation.UpdatedAt should be updated
        _repo.Verify(r => r.UpdateAsync(It.Is<Conversation>(c => c.UpdatedAt >= before), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMessageAsync_NonExistentConversation_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.AddMessageAsync(Guid.NewGuid(), ConversationRole.User, "hello"));
    }

    [Fact]
    public async Task GetConversationAsync_DelegatesToRepository()
    {
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var result = await _sut.GetConversationAsync(convId);

        Assert.NotNull(result);
        Assert.Equal(convId, result!.Id);
    }

    [Fact]
    public async Task GetConversationWithMessagesAsync_DelegatesToRepository()
    {
        var convId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdWithMessagesAsync(convId, 25, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        var result = await _sut.GetConversationWithMessagesAsync(convId, 25, 10);

        Assert.NotNull(result);
        _repo.Verify(r => r.GetByIdWithMessagesAsync(convId, 25, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListConversationsAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        _repo.Setup(r => r.ListAsync(spokeId, 30, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());

        var result = await _sut.ListConversationsAsync(spokeId, 30, 5);

        Assert.Empty(result);
        _repo.Verify(r => r.ListAsync(spokeId, 30, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConversationCountAsync_DelegatesToRepository()
    {
        _repo.Setup(r => r.CountAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await _sut.GetConversationCountAsync();

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task GetMessageCountAsync_DelegatesToRepository()
    {
        var convId = Guid.NewGuid();
        _repo.Setup(r => r.CountMessagesAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(15);

        var result = await _sut.GetMessageCountAsync(convId);

        Assert.Equal(15, result);
    }

    [Fact]
    public async Task SetCcSessionIdAsync_UpdatesConversation()
    {
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        await _sut.SetCcSessionIdAsync(convId, "session-123");

        Assert.Equal("session-123", conversation.CcSessionId);
        _repo.Verify(r => r.UpdateAsync(It.Is<Conversation>(c => c.CcSessionId == "session-123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetCcSessionIdAsync_NonExistent_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.SetCcSessionIdAsync(Guid.NewGuid(), "session-123"));
    }
}
