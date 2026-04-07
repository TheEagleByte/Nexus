namespace Nexus.Spoke.Models;

public class SpokeConfiguration
{
    public SpokeIdentityConfig Spoke { get; set; } = new();
    public HubConnectionConfig Hub { get; set; } = new();
    public CapabilitiesConfig Capabilities { get; set; } = new();
    public WorkspaceConfig Workspace { get; set; } = new();
    public JiraIntegrationConfig Jira { get; set; } = new();
    public ApprovalConfig Approval { get; set; } = new();
    public GitConfig Git { get; set; } = new();
    public DockerConfig Docker { get; set; } = new();

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

    public class JiraIntegrationConfig
    {
        public string InstanceUrl { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string[] ProjectKeys { get; set; } = [];
    }

    public class ApprovalConfig
    {
        public string Mode { get; set; } = "plan_review";
        public int BatchSize { get; set; } = 5;
        public int MaxConcurrentJobs { get; set; } = 1;
        public int HeartbeatIntervalSeconds { get; set; } = 30;
    }

    public class GitConfig
    {
        public string UserName { get; set; } = "Nexus Spoke";
        public string UserEmail { get; set; } = "nexus-spoke@noreply.local";
        public string DefaultRepoUrl { get; set; } = string.Empty;
        public string DefaultBranch { get; set; } = "main";
        public int TimeoutSeconds { get; set; } = 120;
    }

    public class DockerConfig
    {
        public string WorkerImage { get; set; } = "nexus-worker:latest";
        public string WorkerDockerfilePath { get; set; } = "worker/Dockerfile";
        public string Registry { get; set; } = string.Empty;
        public DockerResourceLimitsConfig ResourceLimits { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 14400;
        public string ContainerUser { get; set; } = "1000:1000";
        public string NetworkMode { get; set; } = "bridge";
        public bool ReadOnlyRootFs { get; set; } = true;
        public CredentialsConfig Credentials { get; set; } = new();
    }

    public class CredentialsConfig
    {
        public GitCredentialsConfig Git { get; set; } = new();
        public string GhToken { get; set; } = string.Empty;
    }

    public class GitCredentialsConfig
    {
        public string AuthMethod { get; set; } = "token";
        public string SshKeyPath { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    public class DockerResourceLimitsConfig
    {
        public long MemoryBytes { get; set; } = 8_589_934_592;
        public int CpuCount { get; set; } = 2;
        public long DiskLimitBytes { get; set; } = 107_374_182_400;
    }
}
