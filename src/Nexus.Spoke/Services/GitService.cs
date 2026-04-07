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
            logger.LogInformation("Cloning {RepoHost} into {RepoPath}", RedactUrl(repoUrl), repoPath);
            await RunGitAsync(repoPath, ["clone", repoUrl, "."], ct);
        }
        else
        {
            // Verify origin matches expected URL, update if changed
            var (_, currentOrigin, _) = await RunGitAsync(repoPath, ["config", "--get", "remote.origin.url"], ct);
            if (!string.Equals(currentOrigin, repoUrl, StringComparison.Ordinal))
            {
                logger.LogInformation("Updating origin URL from {Old} to {New}",
                    RedactUrl(currentOrigin), RedactUrl(repoUrl));
                await RunGitAsync(repoPath, ["remote", "set-url", "origin", repoUrl], ct);
            }

            logger.LogInformation("Fetching latest from origin in {RepoPath}", repoPath);
            await RunGitAsync(repoPath, ["fetch", "origin"], ct);
        }

        // Ensure local default branch exists and matches remote — -B handles both create and reset
        await RunGitAsync(repoPath, ["checkout", "-B", defaultBranch, $"origin/{defaultBranch}"], ct);

        // Create or reset the feature branch from the default branch tip
        var branchExists = await BranchExistsAsync(repoPath, featureBranchName, ct);
        if (branchExists)
        {
            logger.LogInformation("Feature branch {Branch} already exists, resetting to {Default}",
                featureBranchName, defaultBranch);
        }
        else
        {
            logger.LogInformation("Creating feature branch {Branch}", featureBranchName);
        }
        await RunGitAsync(repoPath, ["checkout", "-B", featureBranchName, $"origin/{defaultBranch}"], ct);

        // Configure git identity (repo-local)
        var gitConfig = config.Value.Git;
        await RunGitAsync(repoPath, ["config", "user.name", gitConfig.UserName], ct);
        await RunGitAsync(repoPath, ["config", "user.email", gitConfig.UserEmail], ct);

        logger.LogInformation("Git workspace ready: {RepoPath} on branch {Branch}", repoPath, featureBranchName);
        return featureBranchName;
    }

    private async Task<bool> BranchExistsAsync(string repoPath, string branchName, CancellationToken ct)
    {
        try
        {
            await RunGitAsync(repoPath, ["rev-parse", "--verify", branchName], ct, logOnFailure: false);
            return true;
        }
        catch (GitOperationException)
        {
            return false;
        }
    }

    internal async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(
        string workingDirectory, string[] arguments, CancellationToken cancellationToken,
        bool logOnFailure = true)
    {
        var argDisplay = string.Join(" ", arguments);
        logger.LogDebug("Running: git {Arguments} in {WorkingDirectory}", argDisplay, workingDirectory);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();

        try
        {
            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdOutTask;
            var stderr = await stdErrTask;

            if (process.ExitCode != 0)
            {
                if (logOnFailure)
                {
                    logger.LogWarning("Git command failed (exit {ExitCode}): git {Arguments}\n{StdErr}",
                        process.ExitCode, argDisplay, stderr);
                }
                throw new GitOperationException(argDisplay, process.ExitCode, stderr.Trim());
            }

            return (process.ExitCode, stdout.Trim(), stderr.Trim());
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }

            throw;
        }
    }

    internal static string RedactUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
            return uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Path, UriFormat.Unescaped);

        return url;
    }
}
