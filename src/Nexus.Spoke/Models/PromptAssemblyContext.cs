namespace Nexus.Spoke.Models;

public record PromptAssemblyContext(
    Guid JobId,
    JobType JobType,
    string ProjectKey,
    string HubContext,
    TicketMetadata? Ticket,
    string? ProjectContextMd,
    string? PlanMd,
    string SpokeSkillsPath,
    string? ProjectSkillsPath
);
