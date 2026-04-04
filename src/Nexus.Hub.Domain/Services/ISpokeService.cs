using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Domain.Services;

public interface ISpokeService
{
    Task<Spoke> RegisterSpokeAsync(string name, JsonDocument capabilities, JsonDocument config, JsonDocument? profile = null);
    Task<Spoke?> GetSpokeAsync(Guid spokeId);
    Task<List<Spoke>> ListSpokesAsync(SpokeStatus? status = null, int limit = 50, int offset = 0);
    Task UpdateSpokeStatusAsync(Guid spokeId, SpokeStatus status);
    Task UpdateSpokeHeartbeatAsync(Guid spokeId);
    Task UpdateSpokeConfigAsync(Guid spokeId, string? name = null, JsonDocument? config = null);
    Task DeleteSpokeAsync(Guid spokeId);
    Task<int> GetSpokeCountAsync(SpokeStatus? status = null);
}
