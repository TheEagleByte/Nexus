using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class MemoryInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public MemoryInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}", "memories");
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_tempDir)!;
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllThreeFiles()
    {
        await MemoryInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        Assert.True(File.Exists(Path.Combine(_tempDir, "global.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "codebase-notes.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "decision-log.md")));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await MemoryInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        // Modify one file
        var globalPath = Path.Combine(_tempDir, "global.md");
        await File.WriteAllTextAsync(globalPath, "Custom content");

        // Re-run — should NOT overwrite
        await MemoryInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var content = await File.ReadAllTextAsync(globalPath);
        Assert.Equal("Custom content", content);
    }

    [Fact]
    public async Task InitializeAsync_TemplatesHaveExpectedHeaders()
    {
        await MemoryInitializer.InitializeAsync(_tempDir, NullLogger.Instance);

        var global = await File.ReadAllTextAsync(Path.Combine(_tempDir, "global.md"));
        Assert.StartsWith("# Global Knowledge", global);

        var codebase = await File.ReadAllTextAsync(Path.Combine(_tempDir, "codebase-notes.md"));
        Assert.StartsWith("# Codebase Notes", codebase);

        var decisions = await File.ReadAllTextAsync(Path.Combine(_tempDir, "decision-log.md"));
        Assert.StartsWith("# Decision Log", decisions);
    }
}
