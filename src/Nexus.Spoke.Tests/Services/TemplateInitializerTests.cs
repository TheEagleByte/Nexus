using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class TemplateInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public TemplateInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}", "templates");
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_tempDir)!;
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllTemplateFiles()
    {
        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(_tempDir, "worker-prompt-base.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "plan-template.md")));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var basePath = Path.Combine(_tempDir, "worker-prompt-base.md");
        await File.WriteAllTextAsync(basePath, "Custom content");

        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var content = await File.ReadAllTextAsync(basePath);
        Assert.Equal("Custom content", content);
    }

    [Fact]
    public async Task InitializeAsync_WorkerTemplateHasExpectedPlaceholders()
    {
        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "worker-prompt-base.md"));

        Assert.Contains("{TICKET_SUMMARY}", content);
        Assert.Contains("{TICKET_DESCRIPTION}", content);
        Assert.Contains("{ACCEPTANCE_CRITERIA}", content);
        Assert.Contains("{JOB_TYPE}", content);
        Assert.Contains("{JOB_ID}", content);
        Assert.Contains("{PROJECT_KEY}", content);
        Assert.Contains("{HUB_CONTEXT}", content);
        Assert.Contains("{PROJECT_CONTEXT}", content);
        Assert.Contains("{IMPLEMENTATION_PLAN}", content);
        Assert.Contains("{PROJECT_HISTORY}", content);
        Assert.Contains("{SKILL_GUIDANCE}", content);
        Assert.Contains("{TIMESTAMP}", content);
    }

    [Fact]
    public async Task InitializeAsync_PlanTemplateHasExpectedPlaceholders()
    {
        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "plan-template.md"));

        Assert.Contains("{TICKET_KEY}", content);
        Assert.Contains("{TICKET_SUMMARY}", content);
        Assert.Contains("{TICKET_DESCRIPTION}", content);
        Assert.Contains("{ACCEPTANCE_CRITERIA}", content);
    }

    [Fact]
    public async Task InitializeAsync_WorkerTemplateStartsWithHeader()
    {
        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "worker-prompt-base.md"));
        Assert.StartsWith("# Worker Prompt", content);
    }

    [Fact]
    public async Task InitializeAsync_RespectsExistingDirectory()
    {
        Directory.CreateDirectory(_tempDir);

        await TemplateInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(_tempDir, "worker-prompt-base.md")));
    }
}
