using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Hubs;

public class SpokeTimeoutServiceTests
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly Mock<ILogger<SpokeTimeoutService>> _loggerMock = new();
    private readonly SpokeTimeoutService _service;

    public SpokeTimeoutServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => _spokeServiceMock.Object);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _service = new SpokeTimeoutService(scopeFactory, _loggerMock.Object);
    }

    private static Spoke CreateSpoke(Guid id, string name, SpokeStatus status, DateTimeOffset lastSeen)
    {
        return new Spoke
        {
            Id = id,
            Name = name,
            Status = status,
            LastSeen = lastSeen,
            Capabilities = JsonSerializer.SerializeToDocument(Array.Empty<string>()),
            Config = JsonSerializer.SerializeToDocument(new { }),
            CreatedAt = lastSeen,
            UpdatedAt = lastSeen
        };
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_StaleOnlineSpoke_MarksOffline()
    {
        var spokeId = Guid.NewGuid();
        var staleSpoke = CreateSpoke(spokeId, "stale-spoke", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(120));

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([staleSpoke]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.CheckForTimedOutSpokesAsync();

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_StaleBusySpoke_MarksOffline()
    {
        var spokeId = Guid.NewGuid();
        var staleSpoke = CreateSpoke(spokeId, "busy-stale", SpokeStatus.Busy,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(100));

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([staleSpoke]);

        await _service.CheckForTimedOutSpokesAsync();

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_RecentSpoke_NotMarkedOffline()
    {
        var spokeId = Guid.NewGuid();
        var freshSpoke = CreateSpoke(spokeId, "fresh-spoke", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(10));

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([freshSpoke]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.CheckForTimedOutSpokesAsync();

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_NoSpokes_NoAction()
    {
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.CheckForTimedOutSpokesAsync();

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(It.IsAny<Guid>(), It.IsAny<SpokeStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_StatusUpdateFails_ContinuesWithOthers()
    {
        var spoke1 = CreateSpoke(Guid.NewGuid(), "spoke-1", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(120));
        var spoke2 = CreateSpoke(Guid.NewGuid(), "spoke-2", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(120));

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([spoke1, spoke2]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _spokeServiceMock
            .Setup(s => s.UpdateSpokeStatusAsync(spoke1.Id, SpokeStatus.Offline, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        await _service.CheckForTimedOutSpokesAsync();

        // Both attempted despite first failure
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spoke1.Id, SpokeStatus.Offline, It.IsAny<CancellationToken>()), Times.Once);
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(spoke2.Id, SpokeStatus.Offline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckForTimedOutSpokes_MixedFreshAndStale_OnlyMarksStale()
    {
        var stale = CreateSpoke(Guid.NewGuid(), "stale", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(120));
        var fresh = CreateSpoke(Guid.NewGuid(), "fresh", SpokeStatus.Online,
            DateTimeOffset.UtcNow - TimeSpan.FromSeconds(10));

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([stale, fresh]);
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Busy, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.CheckForTimedOutSpokesAsync();

        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(stale.Id, SpokeStatus.Offline, It.IsAny<CancellationToken>()), Times.Once);
        _spokeServiceMock.Verify(s => s.UpdateSpokeStatusAsync(fresh.Id, It.IsAny<SpokeStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
