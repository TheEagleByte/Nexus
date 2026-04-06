using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class SkillsInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public SkillsInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}", "skills");
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_tempDir)!;
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    private static SpokeConfiguration CreateConfig(
        string name = "TestSpoke", string os = "Linux", string arch = "x64",
        bool docker = true, bool git = true, bool jira = false, bool prMonitoring = false) =>
        new()
        {
            Spoke = new SpokeConfiguration.SpokeIdentityConfig
            {
                Name = name,
                Os = os,
                Architecture = arch
            },
            Capabilities = new SpokeConfiguration.CapabilitiesConfig
            {
                Docker = docker,
                Git = git,
                Jira = jira,
                PrMonitoring = prMonitoring
            }
        };

    [Fact]
    public async Task InitializeAsync_CreatesClaudeMdFile()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(_tempDir, "CLAUDE.md")));
    }

    [Fact]
    public async Task InitializeAsync_CreatesSubdirectories()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "conventions")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "templates")));
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllSkillFiles()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(_tempDir, "conventions", "workspace-management.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "conventions", "job-orchestration.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "conventions", "conversation-management.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "templates", "implementation-plan.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "templates", "job-summary.md")));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        // Modify CLAUDE.md
        var claudeMdPath = Path.Combine(_tempDir, "CLAUDE.md");
        await File.WriteAllTextAsync(claudeMdPath, "Custom content");

        // Re-run — should NOT overwrite
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        var content = await File.ReadAllTextAsync(claudeMdPath);
        Assert.Equal("Custom content", content);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotOverwriteCustomizedSkills()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        var skillPath = Path.Combine(_tempDir, "conventions", "workspace-management.md");
        await File.WriteAllTextAsync(skillPath, "Custom workspace management");

        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        var content = await File.ReadAllTextAsync(skillPath);
        Assert.Equal("Custom workspace management", content);
    }

    [Fact]
    public async Task InitializeAsync_IncludesSpokeNameInTemplate()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(name: "MyTestSpoke"), NullLogger.Instance);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CLAUDE.md"));
        Assert.Contains("MyTestSpoke", content);
    }

    [Fact]
    public async Task InitializeAsync_IncludesEnabledCapabilities()
    {
        await SkillsInitializer.InitializeAsync(
            _tempDir, CreateConfig(docker: true, git: true, jira: false), NullLogger.Instance);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CLAUDE.md"));
        Assert.Contains("Docker", content);
        Assert.Contains("Git", content);
        Assert.DoesNotContain("Jira", content);
    }

    [Fact]
    public async Task InitializeAsync_HandlesMissingOptionalConfig()
    {
        var emptyConfig = new SpokeConfiguration();

        await SkillsInitializer.InitializeAsync(_tempDir, emptyConfig, NullLogger.Instance);

        // Should not throw, and CLAUDE.md should still be created
        Assert.True(File.Exists(Path.Combine(_tempDir, "CLAUDE.md")));
    }

    [Fact]
    public async Task InitializeAsync_SkillFilesHaveExpectedHeaders()
    {
        await SkillsInitializer.InitializeAsync(_tempDir, CreateConfig(), NullLogger.Instance);

        var workspace = await File.ReadAllTextAsync(Path.Combine(_tempDir, "conventions", "workspace-management.md"));
        Assert.StartsWith("# Workspace Management", workspace);

        var jobs = await File.ReadAllTextAsync(Path.Combine(_tempDir, "conventions", "job-orchestration.md"));
        Assert.StartsWith("# Job Orchestration", jobs);

        var conversation = await File.ReadAllTextAsync(Path.Combine(_tempDir, "conventions", "conversation-management.md"));
        Assert.StartsWith("# Conversation Management", conversation);

        var plan = await File.ReadAllTextAsync(Path.Combine(_tempDir, "templates", "implementation-plan.md"));
        Assert.StartsWith("# Implementation Plan", plan);

        var summary = await File.ReadAllTextAsync(Path.Combine(_tempDir, "templates", "job-summary.md"));
        Assert.StartsWith("# Job Summary", summary);
    }
}
