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
    public async Task RecordMessageAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.RecordMessageAsync(Guid.NewGuid(), MessageDirection.UserToSpoke, "hello"));
    }

    [Fact]
    public async Task GetConversationAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.GetConversationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMessageCountAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.GetMessageCountAsync(Guid.NewGuid()));
    }
}
