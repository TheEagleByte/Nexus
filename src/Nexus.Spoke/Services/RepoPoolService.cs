using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class RepoPoolService(
    IGitService gitService,
    IOptions<SpokeConfiguration> config,
    ILogger<RepoPoolService> logger) : IRepoPoolService
{
    private readonly ConcurrentDictionary<string, RepoSyncState> _syncStates = new();

    public async Task InitializeAsync(CancellationToken ct)
    {
        var repos = config.Value.GitProvider.Repositories;
        if (repos.Length == 0)
        {
            logger.LogDebug("No repositories configured, skipping initialization");
            return;
        }

        logger.LogInformation("Initializing repo pool with {Count} repositories", repos.Length);

        foreach (var repo in repos)
        {
            if (ct.IsCancellationRequested) break;

            var repoPath = GetRepoPath(repo.Name);

            if (await gitService.IsGitRepoAsync(repoPath, ct))
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.Synced, null, null);
                logger.LogDebug("Repository {Name} already exists at {Path}", repo.Name, repoPath);
                continue;
            }

            _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.Cloning, null, null);
            logger.LogInformation("Cloning repository {Name} from {Url}", repo.Name, repo.RemoteUrl);

            var success = await gitService.CloneAsync(repo.RemoteUrl, repoPath, repo.DefaultBranch, ct);
            if (success)
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.Synced, DateTimeOffset.UtcNow, null);
                logger.LogInformation("Successfully cloned {Name}", repo.Name);
            }
            else
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.CloneFailed, null, "Clone failed");
                logger.LogWarning("Failed to clone {Name} from {Url}", repo.Name, repo.RemoteUrl);
            }
        }
    }

    public async Task SyncAllAsync(CancellationToken ct)
    {
        var repos = config.Value.GitProvider.Repositories;
        if (repos.Length == 0) return;

        logger.LogDebug("Starting repo pool sync for {Count} repositories", repos.Length);

        foreach (var repo in repos)
        {
            if (ct.IsCancellationRequested) break;

            var repoPath = GetRepoPath(repo.Name);

            if (!await gitService.IsGitRepoAsync(repoPath, ct))
            {
                logger.LogWarning("Repository {Name} not found at {Path}, skipping sync", repo.Name, repoPath);
                continue;
            }

            var fetchSuccess = await gitService.FetchAsync(repoPath, ct);
            if (!fetchSuccess)
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.SyncFailed, null, "Fetch failed");
                logger.LogWarning("Fetch failed for {Name}", repo.Name);
                continue;
            }

            var branch = repo.DefaultBranch ?? "main";
            var ffSuccess = await gitService.FastForwardAsync(repoPath, branch, ct);
            if (ffSuccess)
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.Synced, DateTimeOffset.UtcNow, null);
            }
            else
            {
                _syncStates[repo.Name] = new RepoSyncState(repo.Name, RepoSyncStatus.SyncFailed, null, "Fast-forward failed");
                logger.LogWarning("Fast-forward failed for {Name} on branch {Branch}", repo.Name, branch);
            }
        }
    }

    public IReadOnlyDictionary<string, RepoSyncState> GetSyncStates() => _syncStates;

    public string GetRepoPath(string repoName)
    {
        var basePath = WorkspaceInitializer.ResolveBasePath(config.Value);
        return Path.Combine(basePath, "repos", repoName);
    }
}
