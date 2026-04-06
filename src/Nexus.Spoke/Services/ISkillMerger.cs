namespace Nexus.Spoke.Services;

public interface ISkillMerger
{
    Task<string?> MergeSkillsAsync(
        string spokeSkillsPath, string? projectSkillsPath,
        CancellationToken cancellationToken = default);
}
