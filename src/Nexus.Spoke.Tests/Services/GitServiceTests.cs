using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class GitServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _remoteDir;
    private readonly string _repoDir;
    private readonly GitService _sut;

    public GitServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-git-test-{Guid.NewGuid():N}");
        _remoteDir = Path.Combine(_tempDir, "remote");
        _repoDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_remoteDir);

        var config = new SpokeConfiguration
        {
            Git = new SpokeConfiguration.GitConfig
            {
                UserName = "Test User",
                UserEmail = "test@nexus.local",
                TimeoutSeconds = 30
            }
        };

        _sut = new GitService(
            Options.Create(config),
            NullLogger<GitService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            // Git files on Windows can be read-only
            foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_ClonesRepoWhenNotPresent()
    {
        await InitBareRemoteAsync();

        await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        Assert.True(Directory.Exists(Path.Combine(_repoDir, ".git")));
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_CreatesFeatureBranch()
    {
        await InitBareRemoteAsync();

        var branch = await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        Assert.Equal("nexus/implement/TEST-1", branch);

        var (_, currentBranch, _) = await _sut.RunGitAsync(_repoDir, "rev-parse --abbrev-ref HEAD", CancellationToken.None);
        Assert.Equal("nexus/implement/TEST-1", currentBranch);
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_PullsLatestWhenAlreadyCloned()
    {
        await InitBareRemoteAsync();

        // First clone
        await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        // Add a new commit to the remote
        var workDir = Path.Combine(_tempDir, "work");
        await RunProcessAsync("git", $"clone {_remoteDir} {workDir}");
        await RunProcessAsync("git", "config user.name \"Work\"", workDir);
        await RunProcessAsync("git", "config user.email \"work@test.local\"", workDir);
        await File.WriteAllTextAsync(Path.Combine(workDir, "new-file.txt"), "new content");
        await RunProcessAsync("git", "add .", workDir);
        await RunProcessAsync("git", "commit -m \"second commit\"", workDir);
        await RunProcessAsync("git", "push", workDir);

        // Second prepare — should fetch and reset
        await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-2");

        Assert.True(File.Exists(Path.Combine(_repoDir, "new-file.txt")));
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_ConfiguresIdentity()
    {
        await InitBareRemoteAsync();

        await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        var (_, userName, _) = await _sut.RunGitAsync(_repoDir, "config user.name", CancellationToken.None);
        var (_, userEmail, _) = await _sut.RunGitAsync(_repoDir, "config user.email", CancellationToken.None);

        Assert.Equal("Test User", userName);
        Assert.Equal("test@nexus.local", userEmail);
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_HandlesExistingFeatureBranch()
    {
        await InitBareRemoteAsync();

        // First prepare creates the branch
        await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        // Second prepare with same branch — should not throw
        var branch = await _sut.PrepareWorkspaceAsync(
            _repoDir, _remoteDir, "main", "nexus/implement/TEST-1");

        Assert.Equal("nexus/implement/TEST-1", branch);
    }

    [Fact]
    public async Task PrepareWorkspaceAsync_ThrowsOnInvalidUrl()
    {
        var ex = await Assert.ThrowsAsync<GitOperationException>(() =>
            _sut.PrepareWorkspaceAsync(
                _repoDir, "/nonexistent/path/to/repo", "main", "nexus/implement/TEST-1"));

        Assert.NotEqual(0, ex.ExitCode);
    }

    private async Task InitBareRemoteAsync()
    {
        await RunProcessAsync("git", $"init --bare --initial-branch=main {_remoteDir}");

        // Create a temporary clone, add initial commit, push to bare remote
        var initDir = Path.Combine(_tempDir, "init");
        await RunProcessAsync("git", $"clone {_remoteDir} {initDir}");
        await RunProcessAsync("git", "config user.name \"Init\"", initDir);
        await RunProcessAsync("git", "config user.email \"init@test.local\"", initDir);
        await RunProcessAsync("git", "checkout -b main", initDir);
        await File.WriteAllTextAsync(Path.Combine(initDir, "README.md"), "# Test Repo");
        await RunProcessAsync("git", "add .", initDir);
        await RunProcessAsync("git", "commit -m \"initial commit\"", initDir);
        await RunProcessAsync("git", "push -u origin main", initDir);
    }

    private static async Task RunProcessAsync(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new Exception($"{fileName} {arguments} failed: {stderr}");
        }
    }
}
