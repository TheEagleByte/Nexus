using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Data;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class SpokeRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NexusDbContext _ctx;
    private readonly SpokeRepository _repo;

    public SpokeRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseSqlite(_connection)
            .Options;

        _ctx = new NexusDbContext(options);
        _ctx.Database.EnsureCreated();
        _repo = new SpokeRepository(_ctx);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    private static Spoke CreateSpoke(SpokeStatus status = SpokeStatus.Online, DateTimeOffset? createdAt = null)
    {
        return new Spoke
        {
            Id = Guid.NewGuid(),
            Name = $"spoke-{Guid.NewGuid():N}",
            Status = status,
            Capabilities = JsonDocument.Parse("{}"),
            Config = JsonDocument.Parse("{}"),
            LastSeen = DateTimeOffset.UtcNow,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task AddAsync_PersistsSpoke()
    {
        var spoke = CreateSpoke();

        var result = await _repo.AddAsync(spoke);

        Assert.Equal(spoke.Id, result.Id);
        Assert.Equal(1, await _ctx.Spokes.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsSpoke()
    {
        var spoke = CreateSpoke();
        await _repo.AddAsync(spoke);

        var result = await _repo.GetByIdAsync(spoke.Id);

        Assert.NotNull(result);
        Assert.Equal(spoke.Id, result!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotExists_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSpokes()
    {
        await _repo.AddAsync(CreateSpoke());
        await _repo.AddAsync(CreateSpoke());

        var result = await _repo.ListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Online));
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Offline));
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Online));

        var result = await _repo.ListAsync(SpokeStatus.Online);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(SpokeStatus.Online, s.Status));
    }

    [Fact]
    public async Task ListAsync_RespectsLimitAndOffset()
    {
        for (int i = 0; i < 5; i++)
            await _repo.AddAsync(CreateSpoke(createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));

        var result = await _repo.ListAsync(limit: 2, offset: 1);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesSpoke()
    {
        var spoke = CreateSpoke(SpokeStatus.Online);
        await _repo.AddAsync(spoke);

        spoke.Status = SpokeStatus.Offline;
        await _repo.UpdateAsync(spoke);

        var updated = await _repo.GetByIdAsync(spoke.Id);
        Assert.Equal(SpokeStatus.Offline, updated!.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSpoke()
    {
        var spoke = CreateSpoke();
        await _repo.AddAsync(spoke);

        await _repo.DeleteAsync(spoke.Id);

        Assert.Equal(0, await _ctx.Spokes.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNothing()
    {
        await _repo.DeleteAsync(Guid.NewGuid()); // should not throw
    }

    [Fact]
    public async Task CountAsync_ReturnsTotal()
    {
        await _repo.AddAsync(CreateSpoke());
        await _repo.AddAsync(CreateSpoke());
        await _repo.AddAsync(CreateSpoke());

        var count = await _repo.CountAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task CountAsync_FiltersByStatus()
    {
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Online));
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Offline));
        await _repo.AddAsync(CreateSpoke(SpokeStatus.Online));

        var count = await _repo.CountAsync(SpokeStatus.Offline);

        Assert.Equal(1, count);
    }
}
