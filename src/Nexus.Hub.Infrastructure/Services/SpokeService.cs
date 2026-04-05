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

    public Task<Spoke> RegisterSpokeAsync(string name, JsonDocument capabilities, JsonDocument config, JsonDocument? profile = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

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
