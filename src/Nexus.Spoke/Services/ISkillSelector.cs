using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface ISkillSelector
{
    Task<string> SelectAndSummarizeAsync(
        string spokeSkillsPath,
        string? projectSkillsPath,
        JobType jobType,
        string? description,
        CancellationToken cancellationToken = default);
}
