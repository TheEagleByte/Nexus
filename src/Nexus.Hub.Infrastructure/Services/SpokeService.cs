using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class SpokeService(ISpokeRepository spokeRepository, ILogger<SpokeService> logger) : ISpokeService
{
    private readonly ISpokeRepository _spokeRepository = spokeRepository;
    private readonly ILogger<SpokeService> _logger = logger;

    public async Task<Spoke> RegisterSpokeAsync(string name, JsonDocument capabilities, JsonDocument config, JsonDocument? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(config);

        var now = DateTimeOffset.UtcNow;
        var spoke = new Spoke
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = SpokeStatus.Online,
            Capabilities = capabilities,
            Config = config,
            Profile = profile,
            LastSeen = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _spokeRepository.AddAsync(spoke, cancellationToken);
        _logger.LogInformation("Spoke registered: {SpokeId} ({SpokeName})", spoke.Id, spoke.Name);
        return spoke;
    }

    public Task<Spoke?> GetSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Spoke>> ListSpokesAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateSpokeStatusAsync(Guid spokeId, SpokeStatus status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateSpokeHeartbeatAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateSpokeConfigAsync(Guid spokeId, string? name = null, JsonDocument? config = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetSpokeCountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
