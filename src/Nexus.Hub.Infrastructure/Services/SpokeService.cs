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

    public async Task<Spoke?> GetSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
    {
        var spoke = await _spokeRepository.GetByIdAsync(spokeId, cancellationToken);
        if (spoke is null)
        {
            _logger.LogWarning("Spoke not found: {SpokeId}", spokeId);
            throw new Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found");
        }
        return spoke;
    }

    public Task<List<Spoke>> ListSpokesAsync(SpokeStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _spokeRepository.ListAsync(status, limit, offset, cancellationToken);

    public async Task UpdateSpokeStatusAsync(Guid spokeId, SpokeStatus status, CancellationToken cancellationToken = default)
    {
        var spoke = await _spokeRepository.GetByIdAsync(spokeId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found");

        var now = DateTimeOffset.UtcNow;
        spoke.Status = status;
        spoke.LastSeen = now;
        spoke.UpdatedAt = now;

        await _spokeRepository.UpdateAsync(spoke, cancellationToken);
        _logger.LogInformation("Spoke {SpokeId} status updated to {Status}", spokeId, status);
    }

    public async Task UpdateSpokeHeartbeatAsync(Guid spokeId, CancellationToken cancellationToken = default)
    {
        var spoke = await _spokeRepository.GetByIdAsync(spokeId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found");

        var now = DateTimeOffset.UtcNow;
        spoke.LastSeen = now;
        spoke.UpdatedAt = now;

        await _spokeRepository.UpdateAsync(spoke, cancellationToken);
    }

    public async Task UpdateSpokeConfigAsync(Guid spokeId, string? name = null, JsonDocument? config = null, CancellationToken cancellationToken = default)
    {
        var spoke = await _spokeRepository.GetByIdAsync(spokeId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found");

        if (name is not null) spoke.Name = name;
        if (config is not null) spoke.Config = config;
        spoke.UpdatedAt = DateTimeOffset.UtcNow;

        await _spokeRepository.UpdateAsync(spoke, cancellationToken);
        _logger.LogInformation("Spoke {SpokeId} config updated", spokeId);
    }

    public Task DeleteSpokeAsync(Guid spokeId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> GetSpokeCountAsync(SpokeStatus? status = null, CancellationToken cancellationToken = default)
        => _spokeRepository.CountAsync(status, cancellationToken);
}
