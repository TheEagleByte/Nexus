namespace Nexus.Spoke.Services;

public interface IGitService
{
    Task<bool> CloneAsync(string remoteUrl, string localPath, string? branch = null, CancellationToken ct = default);
    Task<bool> FetchAsync(string repoPath, CancellationToken ct = default);
    Task<bool> FastForwardAsync(string repoPath, string branch, CancellationToken ct = default);
    Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default);
    Task<bool> IsGitRepoAsync(string path, CancellationToken ct = default);
}
