using System.Text.Json;
using Nexus.Hub.Api.Tests.Helpers;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class RepositoryStubTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static JsonDocument EmptyJson() => JsonDocument.Parse("{}");

    private Spoke CreateSpoke(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "test-spoke",
        Status = SpokeStatus.Online,
        LastSeen = DateTimeOffset.UtcNow,
        Capabilities = EmptyJson(),
        Config = EmptyJson(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private Project CreateProject(Guid spokeId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        SpokeId = spokeId,
        Name = "test-project",
        Status = ProjectStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private Job CreateJob(Guid projectId, Guid spokeId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        ProjectId = projectId,
        SpokeId = spokeId,
        Type = JobType.Implement,
        Status = JobStatus.Queued,
        CreatedAt = DateTimeOffset.UtcNow
    };

    // ==========================================================================
    // SpokeRepository — full CRUD
    // ==========================================================================

    [Fact]
    public async Task SpokeRepository_AddAndGetById_RoundTrips()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();

        await repo.AddAsync(spoke);
        var loaded = await repo.GetByIdAsync(spoke.Id);

        Assert.NotNull(loaded);
        Assert.Equal(spoke.Name, loaded.Name);
        Assert.Equal(SpokeStatus.Online, loaded.Status);
    }

    [Fact]
    public async Task SpokeRepository_AddAndGetById_PreservesJsonDocumentColumns()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        spoke.Capabilities = JsonDocument.Parse("{\"languages\":[\"csharp\",\"python\"]}");
        spoke.Config = JsonDocument.Parse("{\"timeout\":30}");
        spoke.Profile = JsonDocument.Parse("{\"version\":\"1.0\"}");

        await repo.AddAsync(spoke);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new SpokeRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(spoke.Id);

        Assert.NotNull(loaded);
        Assert.Equal("csharp", loaded.Capabilities.RootElement.GetProperty("languages")[0].GetString());
        Assert.Equal(30, loaded.Config.RootElement.GetProperty("timeout").GetInt32());
        Assert.NotNull(loaded.Profile);
        Assert.Equal("1.0", loaded.Profile.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task SpokeRepository_GetByIdAsync_ReturnsNullForMissing()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SpokeRepository_ListAsync_ReturnsAllSpokes()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke());
        await repo.AddAsync(CreateSpoke());

        var list = await repo.ListAsync();

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task SpokeRepository_ListAsync_FiltersByStatus()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        var online = CreateSpoke();
        var offline = CreateSpoke();
        offline.Status = SpokeStatus.Offline;
        await repo.AddAsync(online);
        await repo.AddAsync(offline);

        var onlineOnly = await repo.ListAsync(status: SpokeStatus.Online);

        Assert.Single(onlineOnly);
        Assert.Equal(online.Id, onlineOnly[0].Id);
    }

    [Fact]
    public async Task SpokeRepository_UpdateAsync_PersistsChanges()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await repo.AddAsync(spoke);

        spoke.Name = "updated-name";
        spoke.Status = SpokeStatus.Offline;
        await repo.UpdateAsync(spoke);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new SpokeRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(spoke.Id);
        Assert.NotNull(loaded);
        Assert.Equal("updated-name", loaded.Name);
        Assert.Equal(SpokeStatus.Offline, loaded.Status);
    }

    [Fact]
    public async Task SpokeRepository_DeleteAsync_RemovesSpoke()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await repo.AddAsync(spoke);

        await repo.DeleteAsync(spoke.Id);

        var loaded = await repo.GetByIdAsync(spoke.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SpokeRepository_CountAsync_ReturnsCorrectCount()
    {
        using var ctx = _factory.CreateContext();
        var repo = new SpokeRepository(ctx);
        await repo.AddAsync(CreateSpoke());
        await repo.AddAsync(CreateSpoke());

        var count = await repo.CountAsync();

        Assert.Equal(2, count);
    }

    // ==========================================================================
    // JobRepository — implemented methods + stubs
    // ==========================================================================

    [Fact]
    public async Task JobRepository_AddAndGetById_RoundTrips()
    {
        using var ctx = _factory.CreateContext();
        var spokeRepo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await spokeRepo.AddAsync(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        var job = CreateJob(project.Id, spoke.Id);
        await repo.AddAsync(job);

        var loaded = await repo.GetByIdAsync(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(JobType.Implement, loaded.Type);
        Assert.Equal(JobStatus.Queued, loaded.Status);
    }

    [Fact]
    public async Task JobRepository_UpdateAsync_PersistsChanges()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        var job = CreateJob(project.Id, spoke.Id);
        await repo.AddAsync(job);

        job.Status = JobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await repo.UpdateAsync(job);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(JobStatus.Running, loaded.Status);
        Assert.NotNull(loaded.StartedAt);
    }

    [Fact]
    public async Task JobRepository_ListByProjectAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByProjectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task JobRepository_ListAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task JobRepository_CountAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new JobRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    // ==========================================================================
    // ProjectRepository — implemented methods + stubs
    // ==========================================================================

    [Fact]
    public async Task ProjectRepository_GetByIdAsync_ReturnsProject()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var loaded = await repo.GetByIdAsync(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("test-project", loaded.Name);
    }

    [Fact]
    public async Task ProjectRepository_UpdateAsync_PersistsChanges()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        project.Name = "updated-project";
        await repo.UpdateAsync(project);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("updated-project", loaded.Name);
    }

    [Fact]
    public async Task ProjectRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task ProjectRepository_AddAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Project()));
    }

    [Fact]
    public async Task ProjectRepository_GetBySpokeAndExternalKeyAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetBySpokeAndExternalKeyAsync(Guid.NewGuid(), "key"));
    }

    [Fact]
    public async Task ProjectRepository_CountAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountAsync());
    }

    // ==========================================================================
    // MessageRepository — all stubs
    // ==========================================================================

    [Fact]
    public async Task MessageRepository_GetByIdAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListBySpokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MessageRepository_AddAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.AddAsync(new Message()));
    }

    [Fact]
    public async Task MessageRepository_CountBySpokeAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new MessageRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountBySpokeAsync(Guid.NewGuid()));
    }

    // ==========================================================================
    // OutputStreamRepository — implemented methods + stubs
    // ==========================================================================

    [Fact]
    public async Task OutputStreamRepository_AddAndGetNextSequence_Works()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        var job = CreateJob(project.Id, spoke.Id);
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        var repo = new OutputStreamRepository(ctx);
        var stream = new OutputStream
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Sequence = 0,
            Content = "hello world",
            StreamType = "stdout",
            Timestamp = DateTimeOffset.UtcNow
        };
        await repo.AddAsync(stream);

        var nextSeq = await repo.GetNextSequenceAsync(job.Id);
        Assert.Equal(1, nextSeq);
    }

    [Fact]
    public async Task OutputStreamRepository_GetNextSequenceAsync_ReturnsZeroForEmpty()
    {
        using var ctx = _factory.CreateContext();
        var repo = new OutputStreamRepository(ctx);

        var nextSeq = await repo.GetNextSequenceAsync(Guid.NewGuid());
        Assert.Equal(0, nextSeq);
    }

    [Fact]
    public async Task OutputStreamRepository_ListByJobAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.ListByJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutputStreamRepository_CountByJobAsync_ThrowsNotImplementedException()
    {
        using var ctx = _factory.CreateContext();
        var repo = new OutputStreamRepository(ctx);
        await Assert.ThrowsAsync<NotImplementedException>(() => repo.CountByJobAsync(Guid.NewGuid()));
    }
}
