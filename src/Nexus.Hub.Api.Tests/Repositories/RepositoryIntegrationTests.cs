using System.Text.Json;
using Nexus.Hub.Api.Tests.Helpers;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Repositories;

namespace Nexus.Hub.Api.Tests.Repositories;

public class RepositoryIntegrationTests : IDisposable
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

        using var ctx2 = _factory.CreateContext();
        var repo2 = new SpokeRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(spoke.Id);

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

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(job.Id);
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
    public async Task JobRepository_ListByProjectAsync_ReturnsFilteredJobs()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project1 = CreateProject(spoke.Id);
        var project2 = CreateProject(spoke.Id);
        ctx.Projects.AddRange(project1, project2);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        await repo.AddAsync(CreateJob(project1.Id, spoke.Id));
        await repo.AddAsync(CreateJob(project1.Id, spoke.Id));
        await repo.AddAsync(CreateJob(project2.Id, spoke.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var jobs = await repo2.ListByProjectAsync(project1.Id);

        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public async Task JobRepository_ListBySpokeAsync_ReturnsFilteredJobs()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        var project1 = CreateProject(spoke1.Id);
        var project2 = CreateProject(spoke2.Id);
        ctx.Projects.AddRange(project1, project2);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        await repo.AddAsync(CreateJob(project1.Id, spoke1.Id));
        await repo.AddAsync(CreateJob(project1.Id, spoke1.Id));
        await repo.AddAsync(CreateJob(project2.Id, spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var jobs = await repo2.ListBySpokeAsync(spoke1.Id);

        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(spoke1.Id, j.SpokeId));
    }

    [Fact]
    public async Task JobRepository_ListBySpokeAsync_FiltersByStatus()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        var queued = CreateJob(project.Id, spoke.Id);
        queued.Status = JobStatus.Queued;
        await repo.AddAsync(queued);
        var running = CreateJob(project.Id, spoke.Id);
        running.Status = JobStatus.Running;
        await repo.AddAsync(running);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var queuedOnly = await repo2.ListBySpokeAsync(spoke.Id, status: JobStatus.Queued);

        Assert.Single(queuedOnly);
        Assert.Equal(JobStatus.Queued, queuedOnly[0].Status);
    }

    [Fact]
    public async Task JobRepository_ListAsync_FiltersMultipleCriteria()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        var job1 = CreateJob(project.Id, spoke.Id);
        job1.Type = JobType.Implement;
        job1.Status = JobStatus.Queued;
        await repo.AddAsync(job1);
        var job2 = CreateJob(project.Id, spoke.Id);
        job2.Type = JobType.Test;
        job2.Status = JobStatus.Queued;
        await repo.AddAsync(job2);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var filtered = await repo2.ListAsync(spokeId: spoke.Id, status: JobStatus.Queued, type: JobType.Implement);

        Assert.Single(filtered);
        Assert.Equal(JobType.Implement, filtered[0].Type);
    }

    [Fact]
    public async Task JobRepository_CountAsync_ReturnsCorrectCount()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        var project1 = CreateProject(spoke1.Id);
        var project2 = CreateProject(spoke2.Id);
        ctx.Projects.AddRange(project1, project2);
        await ctx.SaveChangesAsync();

        var repo = new JobRepository(ctx);
        await repo.AddAsync(CreateJob(project1.Id, spoke1.Id));
        await repo.AddAsync(CreateJob(project1.Id, spoke1.Id));
        await repo.AddAsync(CreateJob(project1.Id, spoke1.Id));
        await repo.AddAsync(CreateJob(project2.Id, spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new JobRepository(ctx2);
        var count = await repo2.CountAsync(spokeId: spoke1.Id);

        Assert.Equal(3, count);
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

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(project.Id);
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
    public async Task ProjectRepository_AddAndGetById_RoundTrips()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var project = CreateProject(spoke.Id);
        project.ExternalKey = "JIRA-123";
        await repo.AddAsync(project);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("test-project", loaded.Name);
        Assert.Equal("JIRA-123", loaded.ExternalKey);
    }

    [Fact]
    public async Task ProjectRepository_ListBySpokeAsync_ReturnsFilteredProjects()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var projects = await repo2.ListBySpokeAsync(spoke1.Id);

        Assert.Equal(2, projects.Count);
        Assert.All(projects, p => Assert.Equal(spoke1.Id, p.SpokeId));
    }

    [Fact]
    public async Task ProjectRepository_ListBySpokeAsync_FiltersByStatus()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var active = CreateProject(spoke.Id);
        active.Status = ProjectStatus.Active;
        await repo.AddAsync(active);
        var completed = CreateProject(spoke.Id);
        completed.Status = ProjectStatus.Completed;
        await repo.AddAsync(completed);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var activeOnly = await repo2.ListBySpokeAsync(spoke.Id, status: ProjectStatus.Active);

        Assert.Single(activeOnly);
        Assert.Equal(ProjectStatus.Active, activeOnly[0].Status);
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_ReturnsAll()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var all = await repo2.ListAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_FiltersBySpokeAndStatus()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var p1 = CreateProject(spoke1.Id);
        p1.Status = ProjectStatus.Active;
        await repo.AddAsync(p1);
        var p2 = CreateProject(spoke1.Id);
        p2.Status = ProjectStatus.Completed;
        await repo.AddAsync(p2);
        await repo.AddAsync(CreateProject(spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var filtered = await repo2.ListAsync(spokeId: spoke1.Id, status: ProjectStatus.Active);

        Assert.Single(filtered);
        Assert.Equal(spoke1.Id, filtered[0].SpokeId);
        Assert.Equal(ProjectStatus.Active, filtered[0].Status);
    }

    [Fact]
    public async Task ProjectRepository_ListAsync_RespectsLimitAndOffset()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        for (int i = 0; i < 5; i++)
        {
            var p = CreateProject(spoke.Id);
            p.Name = $"project-{i}";
            p.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i);
            await repo.AddAsync(p);
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var page = await repo2.ListAsync(limit: 2, offset: 1);

        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task ProjectRepository_GetBySpokeAndExternalKeyAsync_ReturnsMatch()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var project = CreateProject(spoke.Id);
        project.ExternalKey = "EXT-42";
        await repo.AddAsync(project);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var loaded = await repo2.GetBySpokeAndExternalKeyAsync(spoke.Id, "EXT-42");

        Assert.NotNull(loaded);
        Assert.Equal(project.Id, loaded.Id);
    }

    [Fact]
    public async Task ProjectRepository_GetBySpokeAndExternalKeyAsync_ReturnsNullForMissing()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ProjectRepository(ctx);

        var result = await repo.GetBySpokeAndExternalKeyAsync(Guid.NewGuid(), "nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProjectRepository_CountAsync_ReturnsCorrectCount()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke1.Id));
        await repo.AddAsync(CreateProject(spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var count = await repo2.CountAsync(spokeId: spoke1.Id);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ProjectRepository_CountAsync_FiltersByStatus()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new ProjectRepository(ctx);
        var active = CreateProject(spoke.Id);
        active.Status = ProjectStatus.Active;
        await repo.AddAsync(active);
        var completed = CreateProject(spoke.Id);
        completed.Status = ProjectStatus.Completed;
        await repo.AddAsync(completed);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ProjectRepository(ctx2);
        var count = await repo2.CountAsync(status: ProjectStatus.Active);

        Assert.Equal(1, count);
    }

    // ==========================================================================
    // MessageRepository — full CRUD
    // ==========================================================================

    private Message CreateMessage(Guid spokeId, MessageDirection direction = MessageDirection.UserToSpoke, Guid? jobId = null) => new()
    {
        Id = Guid.NewGuid(),
        SpokeId = spokeId,
        Direction = direction,
        Content = "test message",
        JobId = jobId,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task MessageRepository_AddAndGetById_RoundTrips()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        var message = CreateMessage(spoke.Id);
        await repo.AddAsync(message);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(message.Id);

        Assert.NotNull(loaded);
        Assert.Equal(message.Content, loaded.Content);
        Assert.Equal(MessageDirection.UserToSpoke, loaded.Direction);
    }

    [Fact]
    public async Task MessageRepository_GetByIdAsync_ReturnsNullForMissing()
    {
        using var ctx = _factory.CreateContext();
        var repo = new MessageRepository(ctx);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_ReturnsOrderedByTimestamp()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        var older = CreateMessage(spoke.Id);
        older.Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        older.Content = "first";
        await repo.AddAsync(older);

        var newer = CreateMessage(spoke.Id, MessageDirection.SpokeToUser);
        newer.Timestamp = DateTimeOffset.UtcNow;
        newer.Content = "second";
        await repo.AddAsync(newer);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var list = await repo2.ListBySpokeAsync(spoke.Id);

        Assert.Equal(2, list.Count);
        Assert.Equal("first", list[0].Content);
        Assert.Equal("second", list[1].Content);
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_RespectsLimitAndOffset()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        for (int i = 0; i < 5; i++)
        {
            var msg = CreateMessage(spoke.Id);
            msg.Timestamp = DateTimeOffset.UtcNow.AddMinutes(i);
            msg.Content = $"msg-{i}";
            await repo.AddAsync(msg);
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var page = await repo2.ListBySpokeAsync(spoke.Id, limit: 2, offset: 1);

        Assert.Equal(2, page.Count);
        Assert.Equal("msg-1", page[0].Content);
        Assert.Equal("msg-2", page[1].Content);
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_DeterministicOnSameTimestamp()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        var sameTime = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        for (int i = 0; i < 4; i++)
        {
            var msg = CreateMessage(spoke.Id);
            msg.Timestamp = sameTime;
            msg.Content = $"tied-{i}";
            await repo.AddAsync(msg);
            ids.Add(msg.Id);
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var page1 = await repo2.ListBySpokeAsync(spoke.Id, limit: 2, offset: 0);
        var page2 = await repo2.ListBySpokeAsync(spoke.Id, limit: 2, offset: 2);

        var allIds = page1.Concat(page2).Select(m => m.Id).ToList();
        Assert.Equal(4, allIds.Distinct().Count());
    }

    [Fact]
    public async Task MessageRepository_ListBySpokeAsync_FiltersToCorrectSpoke()
    {
        using var ctx = _factory.CreateContext();
        var spoke1 = CreateSpoke();
        var spoke2 = CreateSpoke();
        ctx.Spokes.AddRange(spoke1, spoke2);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        await repo.AddAsync(CreateMessage(spoke1.Id));
        await repo.AddAsync(CreateMessage(spoke1.Id));
        await repo.AddAsync(CreateMessage(spoke2.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var spoke1Messages = await repo2.ListBySpokeAsync(spoke1.Id);

        Assert.Equal(2, spoke1Messages.Count);
    }

    [Fact]
    public async Task MessageRepository_CountBySpokeAsync_ReturnsCorrectCount()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        await repo.AddAsync(CreateMessage(spoke.Id));
        await repo.AddAsync(CreateMessage(spoke.Id));
        await repo.AddAsync(CreateMessage(spoke.Id));

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var count = await repo2.CountBySpokeAsync(spoke.Id);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task MessageRepository_AddAsync_PersistsJobId()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        var job = CreateJob(project.Id, spoke.Id);
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        var message = CreateMessage(spoke.Id, jobId: job.Id);
        await repo.AddAsync(message);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(message.Id);

        Assert.NotNull(loaded);
        Assert.Equal(job.Id, loaded.JobId);
    }

    [Fact]
    public async Task MessageRepository_AddAsync_SupportsLongContent()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        await ctx.SaveChangesAsync();

        var repo = new MessageRepository(ctx);
        var longContent = new string('x', 100_000);
        var message = CreateMessage(spoke.Id);
        message.Content = longContent;
        await repo.AddAsync(message);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new MessageRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(message.Id);

        Assert.NotNull(loaded);
        Assert.Equal(100_000, loaded.Content.Length);
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
    public async Task OutputStreamRepository_ListByJobAsync_ReturnsOrderedBySequence()
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
        for (int i = 2; i >= 0; i--)
        {
            var stream = new OutputStream
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Sequence = i,
                Content = $"line-{i}",
                StreamType = "stdout",
                Timestamp = DateTimeOffset.UtcNow
            };
            await repo.AddAsync(stream);
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new OutputStreamRepository(ctx2);
        var list = await repo2.ListByJobAsync(job.Id);

        Assert.Equal(3, list.Count);
        Assert.Equal(0, list[0].Sequence);
        Assert.Equal(1, list[1].Sequence);
        Assert.Equal(2, list[2].Sequence);
    }

    [Fact]
    public async Task OutputStreamRepository_ListByJobAsync_RespectsLimitAndOffset()
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
        for (int i = 0; i < 5; i++)
        {
            var stream = new OutputStream
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Sequence = i,
                Content = $"line-{i}",
                StreamType = "stdout",
                Timestamp = DateTimeOffset.UtcNow
            };
            await repo.AddAsync(stream);
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new OutputStreamRepository(ctx2);
        var page = await repo2.ListByJobAsync(job.Id, limit: 2, offset: 1);

        Assert.Equal(2, page.Count);
        Assert.Equal(1, page[0].Sequence);
        Assert.Equal(2, page[1].Sequence);
    }

    [Fact]
    public async Task OutputStreamRepository_ListByJobAsync_FiltersToCorrectJob()
    {
        using var ctx = _factory.CreateContext();
        var spoke = CreateSpoke();
        ctx.Spokes.Add(spoke);
        var project = CreateProject(spoke.Id);
        ctx.Projects.Add(project);
        var job1 = CreateJob(project.Id, spoke.Id);
        var job2 = CreateJob(project.Id, spoke.Id);
        ctx.Jobs.AddRange(job1, job2);
        await ctx.SaveChangesAsync();

        var repo = new OutputStreamRepository(ctx);
        await repo.AddAsync(new OutputStream { Id = Guid.NewGuid(), JobId = job1.Id, Sequence = 0, Content = "j1", StreamType = "stdout", Timestamp = DateTimeOffset.UtcNow });
        await repo.AddAsync(new OutputStream { Id = Guid.NewGuid(), JobId = job1.Id, Sequence = 1, Content = "j1", StreamType = "stdout", Timestamp = DateTimeOffset.UtcNow });
        await repo.AddAsync(new OutputStream { Id = Guid.NewGuid(), JobId = job2.Id, Sequence = 0, Content = "j2", StreamType = "stdout", Timestamp = DateTimeOffset.UtcNow });

        using var ctx2 = _factory.CreateContext();
        var repo2 = new OutputStreamRepository(ctx2);
        var list = await repo2.ListByJobAsync(job1.Id);

        Assert.Equal(2, list.Count);
        Assert.All(list, o => Assert.Equal(job1.Id, o.JobId));
    }

    [Fact]
    public async Task OutputStreamRepository_CountByJobAsync_ReturnsCorrectCount()
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
        for (int i = 0; i < 3; i++)
        {
            await repo.AddAsync(new OutputStream { Id = Guid.NewGuid(), JobId = job.Id, Sequence = i, Content = $"line-{i}", StreamType = "stdout", Timestamp = DateTimeOffset.UtcNow });
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new OutputStreamRepository(ctx2);
        var count = await repo2.CountByJobAsync(job.Id);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task OutputStreamRepository_CountByJobAsync_ReturnsZeroForEmpty()
    {
        using var ctx = _factory.CreateContext();
        var repo = new OutputStreamRepository(ctx);

        var count = await repo.CountByJobAsync(Guid.NewGuid());

        Assert.Equal(0, count);
    }

    // ==========================================================================
    // ConversationRepository
    // ==========================================================================

    [Fact]
    public async Task ConversationRepository_AddAndGetById_RoundTrips()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        var conv = new Conversation
        {
            Id = Guid.NewGuid(), Title = "Test Conversation",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        await repo.AddAsync(conv);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(conv.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Test Conversation", loaded.Title);
    }

    [Fact]
    public async Task ConversationRepository_GetById_ReturnsNullForArchived()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        var conv = new Conversation
        {
            Id = Guid.NewGuid(), Title = "Archived", IsArchived = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        await repo.AddAsync(conv);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var loaded = await repo2.GetByIdAsync(conv.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ConversationRepository_ListAsync_ExcludesArchived()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        await repo.AddAsync(new Conversation { Id = Guid.NewGuid(), Title = "Active", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await repo.AddAsync(new Conversation { Id = Guid.NewGuid(), Title = "Archived", IsArchived = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var list = await repo2.ListAsync();

        Assert.Single(list);
        Assert.Equal("Active", list[0].Title);
    }

    [Fact]
    public async Task ConversationRepository_ListAsync_FiltersBySpokeId()
    {
        using var ctx = _factory.CreateContext();
        var spokeRepo = new SpokeRepository(ctx);
        var spoke = CreateSpoke();
        await spokeRepo.AddAsync(spoke);

        var convRepo = new ConversationRepository(ctx);
        await convRepo.AddAsync(new Conversation { Id = Guid.NewGuid(), SpokeId = spoke.Id, Title = "Spoke Conv", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await convRepo.AddAsync(new Conversation { Id = Guid.NewGuid(), Title = "Hub Conv", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var list = await repo2.ListAsync(spokeId: spoke.Id);

        Assert.Single(list);
        Assert.Equal("Spoke Conv", list[0].Title);
    }

    [Fact]
    public async Task ConversationRepository_CountAsync_ExcludesArchived()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        await repo.AddAsync(new Conversation { Id = Guid.NewGuid(), Title = "A", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await repo.AddAsync(new Conversation { Id = Guid.NewGuid(), Title = "B", IsArchived = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var count = await repo2.CountAsync();

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConversationRepository_AddMessageAndCountMessages()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        var convId = Guid.NewGuid();
        await repo.AddAsync(new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        await repo.AddMessageAsync(new ConversationMessage { Id = Guid.NewGuid(), ConversationId = convId, Role = ConversationRole.User, Content = "Hello", Timestamp = DateTimeOffset.UtcNow });
        await repo.AddMessageAsync(new ConversationMessage { Id = Guid.NewGuid(), ConversationId = convId, Role = ConversationRole.Assistant, Content = "Hi", Timestamp = DateTimeOffset.UtcNow });

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var count = await repo2.CountMessagesAsync(convId);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ConversationRepository_GetByIdWithMessages_ReturnsPaginatedMessages()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        var convId = Guid.NewGuid();
        await repo.AddAsync(new Conversation { Id = convId, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        for (int i = 0; i < 5; i++)
        {
            await repo.AddMessageAsync(new ConversationMessage
            {
                Id = Guid.NewGuid(), ConversationId = convId, Role = ConversationRole.User,
                Content = $"Message {i}", Timestamp = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        var conv = await repo2.GetByIdWithMessagesAsync(convId, messageLimit: 2, messageOffset: 1);

        Assert.NotNull(conv);
        Assert.Equal(2, conv.Messages.Count);
        Assert.Equal("Message 1", conv.Messages.First().Content);
    }

    [Fact]
    public async Task ConversationRepository_UpdateAsync_PersistsChanges()
    {
        using var ctx = _factory.CreateContext();
        var repo = new ConversationRepository(ctx);
        var conv = new Conversation { Id = Guid.NewGuid(), Title = "Original", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await repo.AddAsync(conv);

        conv.Title = "Updated";
        conv.IsArchived = true;
        await repo.UpdateAsync(conv);

        using var ctx2 = _factory.CreateContext();
        var repo2 = new ConversationRepository(ctx2);
        // GetById excludes archived, so query directly
        var loaded = await ctx2.Conversations.FindAsync(conv.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Updated", loaded.Title);
        Assert.True(loaded.IsArchived);
    }
}
