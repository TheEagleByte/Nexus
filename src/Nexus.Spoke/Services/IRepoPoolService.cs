namespace Nexus.Spoke.Services;

public interface IRepoPoolService
{
    Task InitializeAsync(CancellationToken ct);
    Task SyncAllAsync(CancellationToken ct);
    IReadOnlyDictionary<string, RepoSyncState> GetSyncStates();
    string GetRepoPath(string repoName);
}

public record RepoSyncState(
    string Name,
    RepoSyncStatus Status,
    DateTimeOffset? LastSyncedAt,
    string? Error
);

public enum RepoSyncStatus
{
    Pending,
    Cloning,
    Synced,
    SyncFailed,
    CloneFailed
}
