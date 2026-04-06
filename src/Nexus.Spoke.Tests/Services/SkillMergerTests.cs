using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class SkillMergerTests : IDisposable
{
    private readonly string _spokeDir;
    private readonly string _projectDir;
    private readonly SkillMerger _sut;

    public SkillMergerTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
        _spokeDir = Path.Combine(baseDir, "spoke-skills");
        _projectDir = Path.Combine(baseDir, "project-skills");
        Directory.CreateDirectory(_spokeDir);
        Directory.CreateDirectory(_projectDir);
        _sut = new SkillMerger(NullLogger<SkillMerger>.Instance);
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_spokeDir)!;
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    [Fact]
    public async Task MergeSkillsAsync_BothExist_ReturnsConcatenated()
    {
        await File.WriteAllTextAsync(Path.Combine(_spokeDir, "CLAUDE.md"), "Spoke content");
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "CLAUDE.md"), "Project content");

        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.NotNull(result);
        Assert.Contains("Spoke content", result);
        Assert.Contains("Project content", result);
        Assert.Contains("---", result);
        Assert.Contains("# Project-Specific Instructions", result);
    }

    [Fact]
    public async Task MergeSkillsAsync_OnlySpokeExists_ReturnsSpokeContent()
    {
        await File.WriteAllTextAsync(Path.Combine(_spokeDir, "CLAUDE.md"), "Spoke only");

        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.Equal("Spoke only", result);
    }

    [Fact]
    public async Task MergeSkillsAsync_OnlyProjectExists_ReturnsProjectContent()
    {
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "CLAUDE.md"), "Project only");

        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.Equal("Project only", result);
    }

    [Fact]
    public async Task MergeSkillsAsync_NeitherExists_ReturnsNull()
    {
        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.Null(result);
    }

    [Fact]
    public async Task MergeSkillsAsync_ProjectPathIsNull_ReturnsSpokeOnly()
    {
        await File.WriteAllTextAsync(Path.Combine(_spokeDir, "CLAUDE.md"), "Spoke content");

        var result = await _sut.MergeSkillsAsync(_spokeDir, null);

        Assert.Equal("Spoke content", result);
    }

    [Fact]
    public async Task MergeSkillsAsync_EmptyFiles_ReturnsNull()
    {
        await File.WriteAllTextAsync(Path.Combine(_spokeDir, "CLAUDE.md"), "");
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "CLAUDE.md"), "   ");

        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.Null(result);
    }

    [Fact]
    public async Task MergeSkillsAsync_ProjectContentAppearsAfterSpoke()
    {
        await File.WriteAllTextAsync(Path.Combine(_spokeDir, "CLAUDE.md"), "SPOKE_MARKER");
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "CLAUDE.md"), "PROJECT_MARKER");

        var result = await _sut.MergeSkillsAsync(_spokeDir, _projectDir);

        Assert.NotNull(result);
        var spokeIndex = result.IndexOf("SPOKE_MARKER", StringComparison.Ordinal);
        var projectIndex = result.IndexOf("PROJECT_MARKER", StringComparison.Ordinal);
        Assert.True(spokeIndex < projectIndex, "Spoke content should appear before project content");
    }
}
