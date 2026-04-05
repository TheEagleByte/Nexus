using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class WorkspaceInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ResolveBasePath_WithExplicitConfig_UsesConfigValue()
    {
        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig
            {
                BaseDirectory = "/custom/path"
            }
        };

        var result = WorkspaceInitializer.ResolveBasePath(config);
        Assert.Equal("/custom/path", result);
    }

    [Fact]
    public void ResolveBasePath_WithEmptyConfig_FallsBackToOsDefault()
    {
        var config = new SpokeConfiguration();
        var result = WorkspaceInitializer.ResolveBasePath(config);

        Assert.False(string.IsNullOrWhiteSpace(result));
        if (OperatingSystem.IsWindows())
            Assert.Contains("Nexus", result);
        else
            Assert.Contains(".nexus", result);
    }

    [Fact]
    public async Task StartAsync_CreatesAllDirectories()
    {
        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };
        var initializer = CreateInitializer(config);

        await initializer.StartAsync(CancellationToken.None);

        Assert.True(Directory.Exists(_tempDir));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "skills")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "projects")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "logs")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "templates")));
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };
        var initializer = CreateInitializer(config);

        await initializer.StartAsync(CancellationToken.None);
        await initializer.StartAsync(CancellationToken.None);

        Assert.True(Directory.Exists(_tempDir));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "skills")));
    }

    [Fact]
    public async Task StartAsync_PreservesExistingContent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "projects"));
        var testFile = Path.Combine(_tempDir, "projects", "test.txt");
        await File.WriteAllTextAsync(testFile, "existing content");

        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };
        var initializer = CreateInitializer(config);

        await initializer.StartAsync(CancellationToken.None);

        Assert.True(File.Exists(testFile));
        Assert.Equal("existing content", await File.ReadAllTextAsync(testFile));
    }

    private static WorkspaceInitializer CreateInitializer(SpokeConfiguration config)
    {
        return new WorkspaceInitializer(
            Options.Create(config),
            NullLogger<WorkspaceInitializer>.Instance);
    }
}
