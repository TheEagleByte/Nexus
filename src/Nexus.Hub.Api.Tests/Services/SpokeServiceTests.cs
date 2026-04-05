using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class SpokeServiceTests
{
    private readonly Mock<ISpokeRepository> _repo = new();
    private readonly Mock<ILogger<SpokeService>> _logger = new();
    private readonly SpokeService _sut;

    public SpokeServiceTests()
    {
        _sut = new SpokeService(_repo.Object, _logger.Object);
    }

    private static JsonDocument EmptyJson() => JsonDocument.Parse("{}");

    [Fact]
    public async Task RegisterSpokeAsync_CreatesSpoke_ReturnsIt()
    {
        var caps = EmptyJson();
        var config = EmptyJson();

        var result = await _sut.RegisterSpokeAsync("test-spoke", caps, config);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test-spoke", result.Name);
        Assert.Equal(SpokeStatus.Online, result.Status);
        _repo.Verify(r => r.AddAsync(It.IsAny<Spoke>(), default), Times.Once);
    }

    [Fact]
    public async Task RegisterSpokeAsync_WithProfile_SetsProfile()
    {
        var profile = JsonDocument.Parse("{\"icon\":\"bot\"}");

        var result = await _sut.RegisterSpokeAsync("spoke", EmptyJson(), EmptyJson(), profile);

        Assert.NotNull(result.Profile);
    }

    [Fact]
    public async Task RegisterSpokeAsync_NullCapabilities_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.RegisterSpokeAsync("spoke", null!, EmptyJson()));
    }

    [Fact]
    public async Task RegisterSpokeAsync_NullConfig_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.RegisterSpokeAsync("spoke", EmptyJson(), null!));
    }

    [Fact]
    public async Task GetSpokeAsync_Exists_ReturnsSpoke()
    {
        var spokeId = Guid.NewGuid();
        var spoke = new Spoke { Id = spokeId, Name = "test" };
        _repo.Setup(r => r.GetByIdAsync(spokeId, default)).ReturnsAsync(spoke);

        var result = await _sut.GetSpokeAsync(spokeId);

        Assert.Equal(spokeId, result!.Id);
    }

    [Fact]
    public async Task GetSpokeAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetSpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListSpokesAsync_DelegatesToRepository()
    {
        var spokes = new List<Spoke> { new() { Id = Guid.NewGuid(), Name = "s1" } };
        _repo.Setup(r => r.ListAsync(SpokeStatus.Online, 10, 5, default)).ReturnsAsync(spokes);

        var result = await _sut.ListSpokesAsync(SpokeStatus.Online, 10, 5);

        Assert.Single(result);
        _repo.Verify(r => r.ListAsync(SpokeStatus.Online, 10, 5, default), Times.Once);
    }

    [Fact]
    public async Task UpdateSpokeStatusAsync_SpokeExists_UpdatesStatusAndTimestamps()
    {
        var spokeId = Guid.NewGuid();
        var spoke = new Spoke { Id = spokeId, Name = "test", Status = SpokeStatus.Online };
        _repo.Setup(r => r.GetByIdAsync(spokeId, default)).ReturnsAsync(spoke);

        await _sut.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Busy);

        Assert.Equal(SpokeStatus.Busy, spoke.Status);
        _repo.Verify(r => r.UpdateAsync(spoke, default), Times.Once);
    }

    [Fact]
    public async Task UpdateSpokeStatusAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateSpokeStatusAsync(Guid.NewGuid(), SpokeStatus.Offline));
    }

    [Fact]
    public async Task UpdateSpokeHeartbeatAsync_SpokeExists_UpdatesTimestamps()
    {
        var spokeId = Guid.NewGuid();
        var spoke = new Spoke { Id = spokeId, Name = "test", LastSeen = DateTimeOffset.UtcNow.AddMinutes(-5) };
        _repo.Setup(r => r.GetByIdAsync(spokeId, default)).ReturnsAsync(spoke);

        await _sut.UpdateSpokeHeartbeatAsync(spokeId);

        _repo.Verify(r => r.UpdateAsync(spoke, default), Times.Once);
    }

    [Fact]
    public async Task UpdateSpokeHeartbeatAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateSpokeHeartbeatAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateSpokeConfigAsync_UpdatesName()
    {
        var spokeId = Guid.NewGuid();
        var spoke = new Spoke { Id = spokeId, Name = "old-name" };
        _repo.Setup(r => r.GetByIdAsync(spokeId, default)).ReturnsAsync(spoke);

        await _sut.UpdateSpokeConfigAsync(spokeId, name: "new-name");

        Assert.Equal("new-name", spoke.Name);
        _repo.Verify(r => r.UpdateAsync(spoke, default), Times.Once);
    }

    [Fact]
    public async Task UpdateSpokeConfigAsync_UpdatesConfig()
    {
        var spokeId = Guid.NewGuid();
        var spoke = new Spoke { Id = spokeId, Name = "test", Config = EmptyJson() };
        _repo.Setup(r => r.GetByIdAsync(spokeId, default)).ReturnsAsync(spoke);
        var newConfig = JsonDocument.Parse("{\"key\":\"value\"}");

        await _sut.UpdateSpokeConfigAsync(spokeId, config: newConfig);

        Assert.Equal(newConfig, spoke.Config);
    }

    [Fact]
    public async Task UpdateSpokeConfigAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Spoke?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateSpokeConfigAsync(Guid.NewGuid(), name: "test"));
    }

    [Fact]
    public async Task DeleteSpokeAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.DeleteSpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSpokeCountAsync_DelegatesToRepository()
    {
        _repo.Setup(r => r.CountAsync(SpokeStatus.Online, default)).ReturnsAsync(5);

        var result = await _sut.GetSpokeCountAsync(SpokeStatus.Online);

        Assert.Equal(5, result);
    }
}
