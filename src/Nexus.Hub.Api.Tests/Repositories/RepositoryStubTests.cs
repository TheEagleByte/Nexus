using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Data;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class RepositoryStubTests
{
    private static NexusDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new NexusDbContext(options);
    }

    // --- JobRepository stubs ---

    [Fact]
    public async Task JobRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListByProjectAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByProjectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public void JobRepository_AddAsync_IsImplemented()
    {
        // AddAsync is implemented (not a stub) — verified by checking it doesn't throw NotImplementedException.
        // Full integration test requires a real database provider (InMemory doesn't support JsonDocument columns).
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SpokeId = Guid.NewGuid(),
            Type = JobType.Implement,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ex = Record.ExceptionAsync(() => repo.AddAsync(job)).Result;
        Assert.IsNotType<NotImplementedException>(ex);
    }

    [Fact]
    public async Task JobRepository_UpdateAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.UpdateAsync(new Job()));
    }

    [Fact]
    public async Task JobRepository_CountAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    // --- ProjectRepository stubs ---

    [Fact]
    public async Task ProjectRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProjectRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task ProjectRepository_AddAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Project()));
    }

    [Fact]
    public async Task ProjectRepository_UpdateAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.UpdateAsync(new Project()));
    }

    [Fact]
    public async Task ProjectRepository_GetBySpokeAndExternalKeyAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetBySpokeAndExternalKeyAsync(Guid.NewGuid(), "key"));
    }

    [Fact]
    public async Task ProjectRepository_CountAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    // --- MessageRepository stubs ---

    [Fact]
    public async Task MessageRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_AddAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Message()));
    }

    [Fact]
    public async Task MessageRepository_CountBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountBySpokeAsync(Guid.NewGuid()));
    }

    // --- OutputStreamRepository stubs ---

    [Fact]
    public async Task OutputStreamRepository_ListByJobAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutputStreamRepository_AddAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new OutputStream()));
    }

    [Fact]
    public async Task OutputStreamRepository_CountByJobAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountByJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutputStreamRepository_GetNextSequenceAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetNextSequenceAsync(Guid.NewGuid()));
    }
}
