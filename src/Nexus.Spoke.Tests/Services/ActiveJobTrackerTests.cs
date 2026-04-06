using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class ActiveJobTrackerTests
{
    private readonly ActiveJobTracker _sut = new();

    private static ActiveJob CreateJob(Guid? jobId = null) =>
        new(jobId ?? Guid.NewGuid(), Guid.NewGuid(), "test-project",
            "container-abc123", new CancellationTokenSource(), DateTimeOffset.UtcNow);

    [Fact]
    public void TryAdd_NewJob_ReturnsTrue()
    {
        var job = CreateJob();
        Assert.True(_sut.TryAdd(job.JobId, job));
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void TryAdd_DuplicateJob_ReturnsFalse()
    {
        var jobId = Guid.NewGuid();
        var job1 = CreateJob(jobId);
        var job2 = CreateJob(jobId);

        Assert.True(_sut.TryAdd(jobId, job1));
        Assert.False(_sut.TryAdd(jobId, job2));
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void TryRemove_ExistingJob_ReturnsTrueAndJob()
    {
        var job = CreateJob();
        _sut.TryAdd(job.JobId, job);

        Assert.True(_sut.TryRemove(job.JobId, out var removed));
        Assert.NotNull(removed);
        Assert.Equal(job.ContainerId, removed.ContainerId);
        Assert.Equal(0, _sut.Count);
    }

    [Fact]
    public void TryRemove_NonexistentJob_ReturnsFalse()
    {
        Assert.False(_sut.TryRemove(Guid.NewGuid(), out var removed));
        Assert.Null(removed);
    }

    [Fact]
    public void Get_ExistingJob_ReturnsJob()
    {
        var job = CreateJob();
        _sut.TryAdd(job.JobId, job);

        var result = _sut.Get(job.JobId);
        Assert.NotNull(result);
        Assert.Equal(job.ContainerId, result.ContainerId);
    }

    [Fact]
    public void Get_NonexistentJob_ReturnsNull()
    {
        Assert.Null(_sut.Get(Guid.NewGuid()));
    }

    [Fact]
    public void GetAll_ReturnsAllJobs()
    {
        var job1 = CreateJob();
        var job2 = CreateJob();
        _sut.TryAdd(job1.JobId, job1);
        _sut.TryAdd(job2.JobId, job2);

        var all = _sut.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Count_ReflectsActiveJobs()
    {
        Assert.Equal(0, _sut.Count);

        var job1 = CreateJob();
        _sut.TryAdd(job1.JobId, job1);
        Assert.Equal(1, _sut.Count);

        var job2 = CreateJob();
        _sut.TryAdd(job2.JobId, job2);
        Assert.Equal(2, _sut.Count);

        _sut.TryRemove(job1.JobId, out _);
        Assert.Equal(1, _sut.Count);
    }
}
