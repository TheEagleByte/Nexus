using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class GitCliService(
    IOptions<SpokeConfiguration> config,
    ILogger<GitCliService> logger) : IGitService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(120);

    public async Task<bool> CloneAsync(string remoteUrl, string localPath, string? branch = null, CancellationToken ct = default)
    {
        var args = new List<string> { "clone", "--single-branch" };
        if (!string.IsNullOrWhiteSpace(branch))
        {
            args.Add("--branch");
            args.Add(branch);
        }
        args.Add(remoteUrl);
        args.Add(localPath);

        var (exitCode, _, stdErr) = await RunGitAsync(args, workingDirectory: null, ct);
        if (exitCode != 0)
        {
            logger.LogWarning("git clone failed for {Url}: {Error}", remoteUrl, stdErr);
            return false;
        }
        return true;
    }

    public async Task<bool> FetchAsync(string repoPath, CancellationToken ct = default)
    {
        var (exitCode, _, stdErr) = await RunGitAsync(["fetch", "origin"], repoPath, ct);
        if (exitCode != 0)
        {
            logger.LogWarning("git fetch failed in {Path}: {Error}", repoPath, stdErr);
            return false;
        }
        return true;
    }

    public async Task<bool> FastForwardAsync(string repoPath, string branch, CancellationToken ct = default)
    {
        var (exitCode, _, stdErr) = await RunGitAsync(["merge", "--ff-only", $"origin/{branch}"], repoPath, ct);
        if (exitCode != 0)
        {
            logger.LogWarning("git fast-forward failed in {Path} for {Branch}: {Error}", repoPath, branch, stdErr);
            return false;
        }
        return true;
    }

    public async Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default)
    {
        var (exitCode, stdOut, _) = await RunGitAsync(["rev-parse", "--abbrev-ref", "HEAD"], repoPath, ct);
        if (exitCode != 0)
            return null;
        return stdOut.Trim();
    }

    public async Task<bool> IsGitRepoAsync(string path, CancellationToken ct = default)
    {
        if (!Directory.Exists(path))
            return false;
        var (exitCode, _, _) = await RunGitAsync(["rev-parse", "--is-inside-work-tree"], path, ct);
        return exitCode == 0;
    }

    internal async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(
        IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        if (!string.IsNullOrEmpty(workingDirectory))
            startInfo.WorkingDirectory = workingDirectory;

        ConfigureCredentials(startInfo);

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CommandTimeout);

        try
        {
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            return (process.ExitCode, stdOut, stdErr);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("git {Args} timed out after {Timeout}s",
                string.Join(' ', arguments), CommandTimeout.TotalSeconds);
            TryKillProcess(process);
            return (-1, string.Empty, "Command timed out");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to run git {Args}", string.Join(' ', arguments));
            return (-1, string.Empty, ex.Message);
        }
    }

    private void ConfigureCredentials(ProcessStartInfo startInfo)
    {
        var gitCreds = config.Value.Docker.Credentials.Git;

        if (string.Equals(gitCreds.AuthMethod, "ssh", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(gitCreds.SshKeyPath))
        {
            var sshKeyPath = ResolvePath(gitCreds.SshKeyPath);
            startInfo.Environment["GIT_SSH_COMMAND"] =
                $"ssh -i \"{sshKeyPath}\" -o StrictHostKeyChecking=accept-new";
        }
        else if (string.Equals(gitCreds.AuthMethod, "token", StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrWhiteSpace(gitCreds.Token))
        {
            // Pass token via env var; askpass script reads it (no secrets on disk)
            startInfo.Environment["NEXUS_GIT_TOKEN"] = gitCreds.Token;

            if (OperatingSystem.IsWindows())
            {
                var askPassScript = Path.Combine(Path.GetTempPath(), "nexus-git-askpass.cmd");
                if (!File.Exists(askPassScript))
                    File.WriteAllText(askPassScript, "@echo %NEXUS_GIT_TOKEN%\n");
                startInfo.Environment["GIT_ASKPASS"] = askPassScript;
            }
            else
            {
                var askPassScript = Path.Combine(Path.GetTempPath(), "nexus-git-askpass.sh");
                if (!File.Exists(askPassScript))
                {
                    File.WriteAllText(askPassScript, "#!/bin/sh\necho \"$NEXUS_GIT_TOKEN\"\n");
                    File.SetUnixFileMode(askPassScript,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                startInfo.Environment["GIT_ASKPASS"] = askPassScript;
            }

            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        }
    }

    private static string ResolvePath(string path)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path == "~") return userProfile;
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(userProfile, path[2..]);
        return Path.GetFullPath(path);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort
        }
    }
}
