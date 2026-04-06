using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface ISpokeService
{
    Task<Spoke> RegisterSpokeAsync(string name, JsonDocument capabilities, JsonDocument config, JsonDocument? profile = null, Guid? requestedId = null, CancellationToken cancellationToken = default);
    Task<Spoke?> GetSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default);
    Task<List<Spoke>> ListSpokesAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task UpdateSpokeStatusAsync(Guid spokeId, SpokeStatus status, CancellationToken cancellationToken = default);
    Task UpdateSpokeHeartbeatAsync(Guid spokeId, CancellationToken cancellationToken = default);
    Task UpdateSpokeConfigAsync(Guid spokeId, string? name = null, JsonDocument? config = null, CancellationToken cancellationToken = default);
    Task DeleteSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default);
    Task<int> GetSpokeCountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default);
}
