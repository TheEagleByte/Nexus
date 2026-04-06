using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class GitService(
    IOptions<SpokeConfiguration> config,
    ILogger<GitService> logger) : IGitService
{
    public async Task<string> PrepareWorkspaceAsync(
        string repoPath,
        string repoUrl,
        string defaultBranch,
        string featureBranchName,
        CancellationToken cancellationToken = default)
    {
        var timeoutSeconds = config.Value.Git.TimeoutSeconds;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var ct = timeoutCts.Token;

        Directory.CreateDirectory(repoPath);

        var isCloned = Directory.Exists(Path.Combine(repoPath, ".git"));

        if (!isCloned)
        {
            logger.LogInformation("Cloning {RepoUrl} into {RepoPath}", repoUrl, repoPath);
            await RunGitAsync(repoPath, $"clone {repoUrl} .", ct);
        }
        else
        {
            logger.LogInformation("Fetching latest from origin in {RepoPath}", repoPath);
            await RunGitAsync(repoPath, "fetch origin", ct);
        }

        // Ensure we're on the default branch at the latest remote state
        await RunGitAsync(repoPath, $"checkout {defaultBranch}", ct);
        await RunGitAsync(repoPath, $"reset --hard origin/{defaultBranch}", ct);

        // Create or reset the feature branch
        var branchExists = await BranchExistsAsync(repoPath, featureBranchName, ct);
        if (branchExists)
        {
            logger.LogInformation("Feature branch {Branch} already exists, resetting to {Default}",
                featureBranchName, defaultBranch);
            await RunGitAsync(repoPath, $"checkout {featureBranchName}", ct);
            await RunGitAsync(repoPath, $"reset --hard origin/{defaultBranch}", ct);
        }
        else
        {
            logger.LogInformation("Creating feature branch {Branch}", featureBranchName);
            await RunGitAsync(repoPath, $"checkout -b {featureBranchName}", ct);
        }

        // Configure git identity (repo-local)
        var gitConfig = config.Value.Git;
        await RunGitAsync(repoPath, $"config user.name \"{gitConfig.UserName}\"", ct);
        await RunGitAsync(repoPath, $"config user.email \"{gitConfig.UserEmail}\"", ct);

        logger.LogInformation("Git workspace ready: {RepoPath} on branch {Branch}", repoPath, featureBranchName);
        return featureBranchName;
    }

    private async Task<bool> BranchExistsAsync(string repoPath, string branchName, CancellationToken ct)
    {
        try
        {
            await RunGitAsync(repoPath, $"rev-parse --verify {branchName}", ct);
            return true;
        }
        catch (GitOperationException)
        {
            return false;
        }
    }

    internal async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(
        string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        logger.LogDebug("Running: git {Arguments} in {WorkingDirectory}", arguments, workingDirectory);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdOutTask;
        var stderr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning("Git command failed (exit {ExitCode}): git {Arguments}\n{StdErr}",
                process.ExitCode, arguments, stderr);
            throw new GitOperationException(arguments, process.ExitCode, stderr.Trim());
        }

        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }
}
