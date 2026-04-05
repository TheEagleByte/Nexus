namespace Nexus.Spoke.Models;

public class SpokeConfiguration
{
    public SpokeIdentityConfig Spoke { get; set; } = new();
    public HubConnectionConfig Hub { get; set; } = new();
    public CapabilitiesConfig Capabilities { get; set; } = new();
    public WorkspaceConfig Workspace { get; set; } = new();
    public ApprovalConfig Approval { get; set; } = new();

    public class SpokeIdentityConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CcSessionId { get; set; }
        public string Os { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
    }

    public class HubConnectionConfig
    {
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class CapabilitiesConfig
    {
        public bool Jira { get; set; }
        public bool Git { get; set; }
        public bool Docker { get; set; }
        public bool PrMonitoring { get; set; }
    }

    public class WorkspaceConfig
    {
        public string BaseDirectory { get; set; } = string.Empty;
        public string? SkillsDirectory { get; set; }
    }

    public class ApprovalConfig
    {
        public string Mode { get; set; } = "plan_review";
        public int BatchSize { get; set; } = 5;
        public int MaxConcurrentJobs { get; set; } = 1;
        public int HeartbeatIntervalSeconds { get; set; } = 30;
    }
}
