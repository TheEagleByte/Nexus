using System.Text;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class SkillSelector(ILogger<SkillSelector> logger) : ISkillSelector
{
    private const int MaxSkills = 5;
    private const int MaxSummaryChars = 200;
    private const int MaxTotalChars = 2000;
    private const int RecencyDays = 7;
    private const int JobTypeMatchWeight = 3;
    private const int RecencyWeight = 1;

    public async Task<string> SelectAndSummarizeAsync(
        string spokeSkillsPath,
        string? projectSkillsPath,
        JobType jobType,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var skillFiles = EnumerateSkillFiles(spokeSkillsPath, projectSkillsPath);
        if (skillFiles.Count == 0)
            return string.Empty;

        var jobTypeName = jobType.ToString().ToLowerInvariant();
        var descriptionWords = ExtractKeywords(description);
        var now = DateTimeOffset.UtcNow;

        var scored = new List<(FileInfo File, int Score, string Source)>();

        foreach (var (file, source) in skillFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var score = 0;
            var fileName = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant();

            // Read first line for header matching
            string? firstLine = null;
            try
            {
                using var reader = file.OpenText();
                firstLine = await reader.ReadLineAsync(cancellationToken);
            }
            catch (IOException)
            {
                continue;
            }

            var headerLower = (firstLine ?? string.Empty).ToLowerInvariant();

            // Job type match (filename or header)
            if (fileName.Contains(jobTypeName) || headerLower.Contains(jobTypeName))
                score += JobTypeMatchWeight;

            // Keyword overlap with description
            if (descriptionWords.Count > 0)
            {
                var matchableText = fileName + " " + headerLower;
                foreach (var word in descriptionWords)
                {
                    if (matchableText.Contains(word))
                        score++;
                }
            }

            // Recency bonus
            if ((now - file.LastWriteTimeUtc).TotalDays <= RecencyDays)
                score += RecencyWeight;

            if (score > 0)
                scored.Add((file, score, source));
        }

        if (scored.Count == 0)
            return string.Empty;

        var selected = scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.File.LastWriteTimeUtc)
            .Take(MaxSkills)
            .ToList();

        var sb = new StringBuilder();
        var totalChars = 0;

        foreach (var (file, score, source) in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string summary;
            try
            {
                var content = await File.ReadAllTextAsync(file.FullName, cancellationToken);
                summary = ExtractFirstParagraph(content, MaxSummaryChars);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Failed to read skill file {Path}", file.FullName);
                continue;
            }

            var skillName = Path.GetFileNameWithoutExtension(file.Name);
            var entry = $"- **{skillName}** ({source}): {summary}\n";

            if (totalChars + entry.Length > MaxTotalChars)
                break;

            sb.Append(entry);
            totalChars += entry.Length;
        }

        return sb.ToString().TrimEnd();
    }

    private static List<(FileInfo File, string Source)> EnumerateSkillFiles(
        string spokeSkillsPath, string? projectSkillsPath)
    {
        var files = new List<(FileInfo, string)>();

        if (Directory.Exists(spokeSkillsPath))
        {
            foreach (var file in new DirectoryInfo(spokeSkillsPath).EnumerateFiles("*.md", SearchOption.AllDirectories))
                files.Add((file, "spoke"));
        }

        if (!string.IsNullOrEmpty(projectSkillsPath) && Directory.Exists(projectSkillsPath))
        {
            foreach (var file in new DirectoryInfo(projectSkillsPath).EnumerateFiles("*.md", SearchOption.AllDirectories))
                files.Add((file, "project"));
        }

        return files;
    }

    private static HashSet<string> ExtractKeywords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .ToLowerInvariant()
            .Split([' ', '\n', '\r', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '-', '_', '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4) // Skip short words
            .ToHashSet();
    }

    private static string ExtractFirstParagraph(string content, int maxChars)
    {
        var lines = content.Split('\n');
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip header lines
            if (trimmed.StartsWith('#'))
                continue;

            // Skip empty lines before content
            if (sb.Length == 0 && trimmed.Length == 0)
                continue;

            // End on blank line after content (paragraph break)
            if (sb.Length > 0 && trimmed.Length == 0)
                break;

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(trimmed);

            if (sb.Length >= maxChars)
                break;
        }

        var result = sb.ToString();
        if (result.Length > maxChars)
            result = result[..(maxChars - 3)] + "...";

        return result;
    }
}
