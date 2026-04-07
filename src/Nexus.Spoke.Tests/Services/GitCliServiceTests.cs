using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class GitCliServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GitCliService _service;

    public GitCliServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new SpokeConfiguration
        {
            Docker = new SpokeConfiguration.DockerConfig
            {
                Credentials = new SpokeConfiguration.CredentialsConfig
                {
                    Git = new SpokeConfiguration.GitCredentialsConfig
                    {
                        AuthMethod = "token",
                        Token = ""
                    }
                }
            }
        };

        _service = new GitCliService(
            Options.Create(config),
            NullLogger<GitCliService>.Instance);
    }

    [Fact]
    public async Task IsGitRepoAsync_NonExistentPath_ReturnsFalse()
    {
        var result = await _service.IsGitRepoAsync(Path.Combine(_tempDir, "nonexistent"));
        Assert.False(result);
    }

    [Fact]
    public async Task IsGitRepoAsync_NonGitDirectory_ReturnsFalse()
    {
        var dir = Path.Combine(_tempDir, "not-git");
        Directory.CreateDirectory(dir);
        var result = await _service.IsGitRepoAsync(dir);
        Assert.False(result);
    }

    [Fact]
    public async Task FetchAsync_NotAGitRepo_ReturnsFalse()
    {
        var dir = Path.Combine(_tempDir, "not-git-fetch");
        Directory.CreateDirectory(dir);
        var result = await _service.FetchAsync(dir);
        Assert.False(result);
    }

    [Fact]
    public async Task FastForwardAsync_NotAGitRepo_ReturnsFalse()
    {
        var dir = Path.Combine(_tempDir, "not-git-ff");
        Directory.CreateDirectory(dir);
        var result = await _service.FastForwardAsync(dir, "main");
        Assert.False(result);
    }

    [Fact]
    public async Task GetCurrentBranchAsync_NotAGitRepo_ReturnsNull()
    {
        var dir = Path.Combine(_tempDir, "not-git-branch");
        Directory.CreateDirectory(dir);
        var result = await _service.GetCurrentBranchAsync(dir);
        Assert.Null(result);
    }

    [Fact]
    public async Task CloneAsync_InvalidUrl_ReturnsFalse()
    {
        var target = Path.Combine(_tempDir, "clone-target");
        var missingRemote = new Uri(Path.Combine(_tempDir, "missing-remote")).AbsoluteUri;
        var result = await _service.CloneAsync(missingRemote, target);
        Assert.False(result);
    }

    [Fact]
    public async Task RunGitAsync_ReturnsExitCodeAndOutput()
    {
        var (exitCode, stdOut, _) = await _service.RunGitAsync(["--version"], null, CancellationToken.None);
        Assert.Equal(0, exitCode);
        Assert.Contains("git version", stdOut);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }
}
