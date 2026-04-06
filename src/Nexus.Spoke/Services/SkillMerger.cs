namespace Nexus.Spoke.Services;

public class SkillMerger(ILogger<SkillMerger> logger) : ISkillMerger
{
    public async Task<string?> MergeSkillsAsync(
        string spokeSkillsPath, string? projectSkillsPath,
        CancellationToken cancellationToken = default)
    {
        string? spokeContent = null;
        string? projectContent = null;

        var spokeClaudeMd = Path.Combine(spokeSkillsPath, "CLAUDE.md");
        if (File.Exists(spokeClaudeMd))
        {
            spokeContent = await File.ReadAllTextAsync(spokeClaudeMd, cancellationToken);
            if (string.IsNullOrWhiteSpace(spokeContent))
                spokeContent = null;
            else
                logger.LogDebug("Loaded spoke skills from {Path}", spokeClaudeMd);
        }

        if (!string.IsNullOrEmpty(projectSkillsPath))
        {
            var projectClaudeMd = Path.Combine(projectSkillsPath, "CLAUDE.md");
            if (File.Exists(projectClaudeMd))
            {
                projectContent = await File.ReadAllTextAsync(projectClaudeMd, cancellationToken);
                if (string.IsNullOrWhiteSpace(projectContent))
                    projectContent = null;
                else
                    logger.LogDebug("Loaded project skills from {Path}", projectClaudeMd);
            }
        }

        if (spokeContent is not null && projectContent is not null)
        {
            logger.LogDebug("Merging spoke and project skills");
            return $"{spokeContent}\n\n---\n\n# Project-Specific Instructions\n\n{projectContent}";
        }

        if (spokeContent is not null)
            return spokeContent;

        if (projectContent is not null)
            return projectContent;

        logger.LogDebug("No skills files found for merging");
        return null;
    }
}
