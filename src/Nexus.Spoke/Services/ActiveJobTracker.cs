using System.Collections.Concurrent;

namespace Nexus.Spoke.Services;

public record ActiveJob(
    Guid JobId,
    Guid ProjectId,
    string ProjectKey,
    string ContainerId,
    CancellationTokenSource Cts,
    DateTimeOffset StartedAt);

public class ActiveJobTracker
{
    private readonly ConcurrentDictionary<Guid, ActiveJob> _jobs = new();

    public int Count => _jobs.Count;

    public bool TryAdd(Guid jobId, ActiveJob job) => _jobs.TryAdd(jobId, job);

    public bool TryRemove(Guid jobId, out ActiveJob? job)
    {
        var result = _jobs.TryRemove(jobId, out var removed);
        job = removed;
        return result;
    }

    public ActiveJob? Get(Guid jobId) => _jobs.GetValueOrDefault(jobId);

    public IReadOnlyList<ActiveJob> GetAll() => _jobs.Values.ToList().AsReadOnly();
}
