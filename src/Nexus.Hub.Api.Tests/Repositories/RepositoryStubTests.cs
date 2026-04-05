using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Data;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class RepositoryStubTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NexusDbContext _ctx;

    public RepositoryStubTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseSqlite(_connection)
            .Options;

        _ctx = new NexusDbContext(options);
        _ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    // --- JobRepository stubs ---

    [Fact]
    public async Task JobRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListByProjectAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByProjectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task JobRepository_UpdateAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.UpdateAsync(new Job()));
    }

    [Fact]
    public async Task JobRepository_CountAsync_ThrowsNotImplementedException()
    {
        var repo = new JobRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    [Fact]
    public async Task JobRepository_AddAsync_PersistsJob()
    {
        var repo = new JobRepository(_ctx);
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        // Need parent entities for FK constraints
        _ctx.Spokes.Add(new Spoke
        {
            Id = spokeId,
            Name = "test-spoke",
            Status = SpokeStatus.Online,
            Capabilities = System.Text.Json.JsonDocument.Parse("{}"),
            Config = System.Text.Json.JsonDocument.Parse("{}"),
            LastSeen = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _ctx.Projects.Add(new Project
        {
            Id = projectId,
            SpokeId = spokeId,
            Name = "test-project",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _ctx.SaveChangesAsync();

        var job = new Job
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            ProjectId = projectId,
            Type = JobType.Implement,
            Status = JobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await repo.AddAsync(job);

        Assert.Equal(job.Id, result.Id);
        Assert.Equal(1, await _ctx.Jobs.CountAsync());
    }

    // --- ProjectRepository stubs ---

    [Fact]
    public async Task ProjectRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProjectRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task ProjectRepository_AddAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Project()));
    }

    [Fact]
    public async Task ProjectRepository_UpdateAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.UpdateAsync(new Project()));
    }

    [Fact]
    public async Task ProjectRepository_GetBySpokeAndExternalKeyAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetBySpokeAndExternalKeyAsync(Guid.NewGuid(), "key"));
    }

    [Fact]
    public async Task ProjectRepository_CountAsync_ThrowsNotImplementedException()
    {
        var repo = new ProjectRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    // --- MessageRepository stubs ---

    [Fact]
    public async Task MessageRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        var repo = new MessageRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        var repo = new MessageRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_AddAsync_ThrowsNotImplementedException()
    {
        var repo = new MessageRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Message()));
    }

    [Fact]
    public async Task MessageRepository_CountBySpokeAsync_ThrowsNotImplementedException()
    {
        var repo = new MessageRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountBySpokeAsync(Guid.NewGuid()));
    }

    // --- OutputStreamRepository stubs ---

    [Fact]
    public async Task OutputStreamRepository_ListByJobAsync_ThrowsNotImplementedException()
    {
        var repo = new OutputStreamRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutputStreamRepository_AddAsync_ThrowsNotImplementedException()
    {
        var repo = new OutputStreamRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new OutputStream()));
    }

    [Fact]
    public async Task OutputStreamRepository_CountByJobAsync_ThrowsNotImplementedException()
    {
        var repo = new OutputStreamRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountByJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutputStreamRepository_GetNextSequenceAsync_ThrowsNotImplementedException()
    {
        var repo = new OutputStreamRepository(_ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetNextSequenceAsync(Guid.NewGuid()));
    }
}
