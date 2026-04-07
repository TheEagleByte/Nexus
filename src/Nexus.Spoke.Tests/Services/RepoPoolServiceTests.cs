using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class RepoPoolServiceTests : IDisposable
{
    private readonly Mock<IGitService> _mockGit = new();
    private readonly string _tempDir;
    private readonly SpokeConfiguration _config;

    public RepoPoolServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-pool-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "repos"));

        _config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Git = true },
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir },
            GitProvider = new SpokeConfiguration.GitProviderConfig
            {
                Repositories =
                [
                    new SpokeConfiguration.RepositoryConfig
                    {
                        Name = "repo-a",
                        RemoteUrl = "git@github.com:org/repo-a.git",
                        DefaultBranch = "main"
                    },
                    new SpokeConfiguration.RepositoryConfig
                    {
                        Name = "repo-b",
                        RemoteUrl = "git@github.com:org/repo-b.git"
                    }
                ]
            }
        };
    }

    private RepoPoolService CreateService() => new(
        _mockGit.Object,
        Options.Create(_config),
        NullLogger<RepoPoolService>.Instance);

    [Fact]
    public async Task InitializeAsync_ClonesConfiguredRepos()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockGit.Setup(g => g.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.InitializeAsync(CancellationToken.None);

        _mockGit.Verify(g => g.CloneAsync("git@github.com:org/repo-a.git",
            It.Is<string>(p => p.EndsWith("repo-a")), "main", It.IsAny<CancellationToken>()), Times.Once);
        _mockGit.Verify(g => g.CloneAsync("git@github.com:org/repo-b.git",
            It.Is<string>(p => p.EndsWith("repo-b")), null, It.IsAny<CancellationToken>()), Times.Once);

        var states = service.GetSyncStates();
        Assert.Equal(RepoSyncStatus.Synced, states["repo-a"].Status);
        Assert.Equal(RepoSyncStatus.Synced, states["repo-b"].Status);
    }

    [Fact]
    public async Task InitializeAsync_SkipsExistingRepos()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.InitializeAsync(CancellationToken.None);

        _mockGit.Verify(g => g.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_CloneFailure_SetsCloneFailedStatus()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockGit.Setup(g => g.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();
        await service.InitializeAsync(CancellationToken.None);

        var states = service.GetSyncStates();
        Assert.Equal(RepoSyncStatus.CloneFailed, states["repo-a"].Status);
        Assert.Equal("Clone failed", states["repo-a"].Error);
    }

    [Fact]
    public async Task SyncAllAsync_FetchesAndFastForwards()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FastForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.SyncAllAsync(CancellationToken.None);

        _mockGit.Verify(g => g.FetchAsync(It.Is<string>(p => p.EndsWith("repo-a")), It.IsAny<CancellationToken>()), Times.Once);
        _mockGit.Verify(g => g.FastForwardAsync(It.Is<string>(p => p.EndsWith("repo-a")), "main", It.IsAny<CancellationToken>()), Times.Once);
        // repo-b has no DefaultBranch set, should default to "main"
        _mockGit.Verify(g => g.FastForwardAsync(It.Is<string>(p => p.EndsWith("repo-b")), "main", It.IsAny<CancellationToken>()), Times.Once);

        var states = service.GetSyncStates();
        Assert.Equal(RepoSyncStatus.Synced, states["repo-a"].Status);
        Assert.NotNull(states["repo-a"].LastSyncedAt);
    }

    [Fact]
    public async Task SyncAllAsync_FetchFailure_SetsSyncFailedAndContinues()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FetchAsync(It.Is<string>(p => p.EndsWith("repo-a")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockGit.Setup(g => g.FetchAsync(It.Is<string>(p => p.EndsWith("repo-b")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FastForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.SyncAllAsync(CancellationToken.None);

        var states = service.GetSyncStates();
        Assert.Equal(RepoSyncStatus.SyncFailed, states["repo-a"].Status);
        Assert.Equal(RepoSyncStatus.Synced, states["repo-b"].Status);
    }

    [Fact]
    public async Task SyncAllAsync_FastForwardFailure_SetsSyncFailed()
    {
        _mockGit.Setup(g => g.IsGitRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGit.Setup(g => g.FastForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();
        await service.SyncAllAsync(CancellationToken.None);

        var states = service.GetSyncStates();
        Assert.Equal(RepoSyncStatus.SyncFailed, states["repo-a"].Status);
        Assert.Equal("Fast-forward failed", states["repo-a"].Error);
    }

    [Fact]
    public async Task InitializeAsync_EmptyRepoList_NoOps()
    {
        _config.GitProvider.Repositories = [];
        var service = CreateService();
        await service.InitializeAsync(CancellationToken.None);

        _mockGit.Verify(g => g.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(service.GetSyncStates());
    }

    [Fact]
    public void GetRepoPath_ReturnsCorrectPath()
    {
        var service = CreateService();
        var path = service.GetRepoPath("my-repo");
        var expected = Path.GetFullPath(Path.Combine(_tempDir, "repos", "my-repo"));
        Assert.Equal(expected, path);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("repo/../../etc")]
    [InlineData("/absolute/path")]
    public void GetRepoPath_PathTraversal_Throws(string name)
    {
        var service = CreateService();
        Assert.Throws<ArgumentException>(() => service.GetRepoPath(name));
    }

    [Fact]
    public void GetRepoPath_EmptyName_Throws()
    {
        var service = CreateService();
        Assert.Throws<ArgumentException>(() => service.GetRepoPath(""));
    }

    [Fact]
    public async Task InitializeAsync_GitDisabled_NoOps()
    {
        _config.Capabilities.Git = false;
        var service = CreateService();
        await service.InitializeAsync(CancellationToken.None);

        _mockGit.Verify(g => g.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }
}
