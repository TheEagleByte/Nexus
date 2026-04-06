namespace Nexus.Spoke.Services;

public interface IGitService
{
    /// <summary>
    /// Prepares a git workspace for a job: clones if needed, pulls latest, creates feature branch,
    /// configures identity. Returns the branch name created.
    /// </summary>
    Task<string> PrepareWorkspaceAsync(
        string repoPath,
        string repoUrl,
        string defaultBranch,
        string featureBranchName,
        CancellationToken cancellationToken = default);
}
