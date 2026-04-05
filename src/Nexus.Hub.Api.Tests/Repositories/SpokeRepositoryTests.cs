using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Data;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class SpokeRepositoryTests
{
    private static NexusDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new NexusDbContext(options);
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
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();

        var result = await repo.AddAsync(spoke);

        Assert.Equal(spoke.Id, result.Id);
        Assert.Equal(1, await ctx.Spokes.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsSpoke()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await repo.AddAsync(spoke);

        var result = await repo.GetByIdAsync(spoke.Id);

        Assert.NotNull(result);
        Assert.Equal(spoke.Id, result!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotExists_ReturnsNull()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSpokes()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke());
        await repo.AddAsync(CreateSpoke());

        var result = await repo.ListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke(SpokeStatus.Online));
        await repo.AddAsync(CreateSpoke(SpokeStatus.Offline));
        await repo.AddAsync(CreateSpoke(SpokeStatus.Online));

        var result = await repo.ListAsync(SpokeStatus.Online);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(SpokeStatus.Online, s.Status));
    }

    [Fact]
    public async Task ListAsync_RespectsLimitAndOffset()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        for (int i = 0; i < 5; i++)
            await repo.AddAsync(CreateSpoke(createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));

        var result = await repo.ListAsync(limit: 2, offset: 1);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesSpoke()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke(SpokeStatus.Online);
        await repo.AddAsync(spoke);

        spoke.Status = SpokeStatus.Offline;
        await repo.UpdateAsync(spoke);

        var updated = await repo.GetByIdAsync(spoke.Id);
        Assert.Equal(SpokeStatus.Offline, updated!.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSpoke()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await repo.AddAsync(spoke);

        await repo.DeleteAsync(spoke.Id);

        Assert.Equal(0, await ctx.Spokes.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNothing()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);

        await repo.DeleteAsync(Guid.NewGuid()); // should not throw
    }

    [Fact]
    public async Task CountAsync_ReturnsTotal()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke());
        await repo.AddAsync(CreateSpoke());
        await repo.AddAsync(CreateSpoke());

        var count = await repo.CountAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task CountAsync_FiltersByStatus()
    {
        using var ctx = CreateInMemoryContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke(SpokeStatus.Online));
        await repo.AddAsync(CreateSpoke(SpokeStatus.Offline));
        await repo.AddAsync(CreateSpoke(SpokeStatus.Online));

        var count = await repo.CountAsync(SpokeStatus.Offline);

        Assert.Equal(1, count);
    }
}
