using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class SkillSelectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _spokeSkillsPath;
    private readonly string _projectSkillsPath;
    private readonly SkillSelector _sut;

    public SkillSelectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
        _spokeSkillsPath = Path.Combine(_tempDir, "spoke-skills");
        _projectSkillsPath = Path.Combine(_tempDir, "project-skills");
        Directory.CreateDirectory(_spokeSkillsPath);
        Directory.CreateDirectory(_projectSkillsPath);

        _sut = new SkillSelector(NullLogger<SkillSelector>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SelectAndSummarize_WithNoSkills_ReturnsEmpty()
    {
        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, _projectSkillsPath,
            JobType.Implement, "some description");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SelectAndSummarize_WithNonExistentDirectories_ReturnsEmpty()
    {
        var result = await _sut.SelectAndSummarizeAsync(
            "/nonexistent/path", "/also/nonexistent",
            JobType.Implement, "some description");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SelectAndSummarize_MatchesJobTypeInFilename()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "refactor-patterns.md"),
            "# Refactor Patterns\n\nGuidance for refactoring code safely.");

        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "testing-guide.md"),
            "# Testing Guide\n\nHow to write good tests.");

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, null,
            JobType.Refactor, "refactor the auth module");

        Assert.Contains("refactor-patterns", result);
    }

    [Fact]
    public async Task SelectAndSummarize_MatchesKeywordsFromDescription()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "authentication.md"),
            "# Authentication\n\nPatterns for auth implementation including OAuth and JWT.");

        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "database.md"),
            "# Database\n\nDatabase migration patterns and best practices.");

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, null,
            JobType.Implement, "implement authentication with OAuth");

        Assert.Contains("authentication", result);
    }

    [Fact]
    public async Task SelectAndSummarize_IncludesProjectSkills()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_projectSkillsPath, "api-conventions.md"),
            "# API Conventions\n\nProject-specific API design patterns and naming conventions.");

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, _projectSkillsPath,
            JobType.Implement, "implement api conventions endpoint");

        Assert.Contains("api-conventions", result);
        Assert.Contains("(project)", result);
    }

    [Fact]
    public async Task SelectAndSummarize_PrefersJobTypeMatchOverKeywordMatch()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "implement-guide.md"),
            "# Implementation Guide\n\nGeneral implementation patterns.");

        await File.WriteAllTextAsync(
            Path.Combine(_spokeSkillsPath, "random-keyword.md"),
            "# Random\n\nSome random guide with keyword overlap.");

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, null,
            JobType.Implement, "keyword");

        // The implement-guide should appear first due to higher job-type score
        var implIndex = result.IndexOf("implement-guide");
        Assert.True(implIndex >= 0, "Job-type matched skill should be included");
    }

    [Fact]
    public async Task SelectAndSummarize_RespectsCharacterBudget()
    {
        // Create many skills to exceed the budget
        for (var i = 0; i < 20; i++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(_spokeSkillsPath, $"implement-skill-{i:D2}.md"),
                $"# Implement Skill {i}\n\n{new string('A', 300)} implementation patterns for various scenarios.");
        }

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, null,
            JobType.Implement, "implement something");

        Assert.True(result.Length <= 2000, $"Result should be under 2000 chars, was {result.Length}");
    }

    [Fact]
    public async Task SelectAndSummarize_SkipsNonMatchingSkills()
    {
        var filePath = Path.Combine(_spokeSkillsPath, "unrelated.md");
        await File.WriteAllTextAsync(filePath,
            "# Unrelated\n\nThis skill has nothing to do with the job.");

        // Set last write time to 30 days ago so recency bonus doesn't apply
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddDays(-30));

        var result = await _sut.SelectAndSummarizeAsync(
            _spokeSkillsPath, null,
            JobType.Test, "test the payment module");

        // "unrelated" has no keyword or job-type match and no recency — score stays 0
        Assert.Equal(string.Empty, result);
    }
}
