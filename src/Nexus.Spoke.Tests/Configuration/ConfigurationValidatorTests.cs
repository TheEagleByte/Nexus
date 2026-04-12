using Nexus.Spoke.Configuration;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Tests.Configuration;

public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    private static SpokeConfiguration CreateValid() => new()
    {
        Spoke = new SpokeConfiguration.SpokeIdentityConfig
        {
            Id = "spoke-001",
            Name = "Test Spoke"
        },
        Hub = new SpokeConfiguration.HubConnectionConfig
        {
            Url = "wss://hub.test/api/hub",
            Token = "test-token"
        }
    };

    [Fact]
    public void Validate_ValidConfig_Succeeds()
    {
        var result = _validator.Validate(null, CreateValid());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MissingSpokeId_Fails()
    {
        var config = CreateValid();
        config.Spoke.Id = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Spoke:Id is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingSpokeName_Fails()
    {
        var config = CreateValid();
        config.Spoke.Name = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Spoke:Name is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingHubUrl_Fails()
    {
        var config = CreateValid();
        config.Hub.Url = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Hub:Url is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingHubToken_Fails()
    {
        var config = CreateValid();
        config.Hub.Token = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Hub:Token is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_InvalidHubUrl_Fails()
    {
        var config = CreateValid();
        config.Hub.Url = "not-a-valid-url";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Hub:Url must be a valid absolute URI", result.FailureMessage);
    }

    [Fact]
    public void Validate_MultipleFieldsMissing_ReportsAll()
    {
        var config = new SpokeConfiguration();
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Spoke:Id", result.FailureMessage);
        Assert.Contains("Spoke:Name", result.FailureMessage);
        Assert.Contains("Hub:Url", result.FailureMessage);
        Assert.Contains("Hub:Token", result.FailureMessage);
    }

    [Fact]
    public void Validate_HeartbeatIntervalTooLow_Fails()
    {
        var config = CreateValid();
        config.Approval.HeartbeatIntervalSeconds = 2;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("HeartbeatIntervalSeconds", result.FailureMessage);
    }

    [Fact]
    public void Validate_MaxConcurrentJobsZero_Fails()
    {
        var config = CreateValid();
        config.Approval.MaxConcurrentJobs = 0;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("MaxConcurrentJobs", result.FailureMessage);
    }

    private static SpokeConfiguration CreateWithDockerAndNetwork(string authMethod = "token")
    {
        var config = CreateValid();
        config.Capabilities.Docker = true;
        config.Docker = new SpokeConfiguration.DockerConfig
        {
            NetworkMode = "bridge",
            TimeoutSeconds = 3600,
            ResourceLimits = new SpokeConfiguration.DockerResourceLimitsConfig
            {
                MemoryBytes = 8_589_934_592,
                CpuCount = 2
            },
            Credentials = new SpokeConfiguration.CredentialsConfig
            {
                Git = new SpokeConfiguration.GitCredentialsConfig
                {
                    AuthMethod = authMethod,
                    Token = authMethod == "token" ? "ghp_test123" : "",
                    SshKeyPath = authMethod == "ssh" ? "~/.ssh/id_ed25519" : "",
                    UserName = "Test User",
                    UserEmail = "test@example.com"
                },
                GhToken = "ghp_gh_token"
            }
        };
        return config;
    }

    [Fact]
    public void Validate_ValidTokenAuth_Succeeds()
    {
        var config = CreateWithDockerAndNetwork("token");
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidSshAuth_Succeeds()
    {
        var config = CreateWithDockerAndNetwork("ssh");
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidAuthMethod_Fails()
    {
        var config = CreateWithDockerAndNetwork("token");
        config.Docker.Credentials.Git.AuthMethod = "invalid";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("AuthMethod must be 'ssh' or 'token'", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingUserName_WhenNetworkEnabled_Fails()
    {
        var config = CreateWithDockerAndNetwork("token");
        config.Docker.Credentials.Git.UserName = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("UserName is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingUserEmail_WhenNetworkEnabled_Fails()
    {
        var config = CreateWithDockerAndNetwork("token");
        config.Docker.Credentials.Git.UserEmail = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("UserEmail is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingSshKeyPath_WhenSshAuth_Fails()
    {
        var config = CreateWithDockerAndNetwork("ssh");
        config.Docker.Credentials.Git.SshKeyPath = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("SshKeyPath is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MissingToken_WhenTokenAuth_Fails()
    {
        var config = CreateWithDockerAndNetwork("token");
        config.Docker.Credentials.Git.Token = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Token is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_NetworkNone_SkipsCredentialValidation()
    {
        var config = CreateWithDockerAndNetwork("token");
        config.Docker.NetworkMode = "none";
        config.Docker.Credentials.Git.UserName = "";
        config.Docker.Credentials.Git.Token = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    // --- GitProvider validation (NEX-187, NEX-190) ---

    private static SpokeConfiguration CreateWithGitProvider() => new()
    {
        Spoke = new SpokeConfiguration.SpokeIdentityConfig { Id = "spoke-001", Name = "Test Spoke" },
        Hub = new SpokeConfiguration.HubConnectionConfig { Url = "wss://hub.test/api/hub", Token = "test-token" },
        Capabilities = new SpokeConfiguration.CapabilitiesConfig { Git = true },
        Docker = new SpokeConfiguration.DockerConfig
        {
            Credentials = new SpokeConfiguration.CredentialsConfig
            {
                Git = new SpokeConfiguration.GitCredentialsConfig
                {
                    AuthMethod = "token",
                    Token = "ghp_test123",
                    UserName = "Test User",
                    UserEmail = "test@example.com"
                }
            }
        },
        GitProvider = new SpokeConfiguration.GitProviderConfig
        {
            Type = "github",
            CredentialsRef = "docker",
            SyncIntervalSeconds = 300,
            BranchTemplate = "nexus/{type}/{key}",
            Repositories =
            [
                new SpokeConfiguration.RepositoryConfig
                {
                    Name = "my-repo",
                    RemoteUrl = "git@github.com:org/my-repo.git"
                }
            ]
        }
    };

    [Fact]
    public void Validate_ValidGitProvider_Succeeds()
    {
        var config = CreateWithGitProvider();
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_GitProvider_InvalidType_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Type = "bitbucket";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:Type must be 'github' or 'gitlab'", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitProvider_EmptyType_Succeeds()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Type = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_GitProvider_RepoMissingName_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories[0].Name = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:Repositories[0]:Name is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitProvider_RepoMissingUrl_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories[0].RemoteUrl = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:Repositories[0]:RemoteUrl is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitProvider_RepoInvalidUrl_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories[0].RemoteUrl = "not-a-url";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("RemoteUrl must be a valid URL or SSH path", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitProvider_RepoHttpsUrl_Succeeds()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories[0].RemoteUrl = "https://github.com/org/repo.git";
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_GitProvider_SyncIntervalTooLow_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.SyncIntervalSeconds = 10;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("SyncIntervalSeconds must be at least 30", result.FailureMessage);
    }

    [Fact]
    public void Validate_BranchTemplate_MissingKeyPlaceholder_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.BranchTemplate = "nexus/{type}/{job-id}";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("BranchTemplate must contain {key} placeholder", result.FailureMessage);
    }

    [Fact]
    public void Validate_BranchTemplate_InvalidChars_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.BranchTemplate = "nexus/{key} branch";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("invalid git branch characters", result.FailureMessage);
    }

    [Fact]
    public void Validate_BranchTemplate_WithAllPlaceholders_Succeeds()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.BranchTemplate = "ai/{type}/{key}/{job-id}";
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("nexus/{key}.lock")]
    [InlineData("nexus/{key}.")]
    [InlineData("nexus/{key}/")]
    public void Validate_BranchTemplate_InvalidEnding_Fails(string template)
    {
        var config = CreateWithGitProvider();
        config.GitProvider.BranchTemplate = template;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("must not end with", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitCapabilityDisabled_SkipsGitProviderValidation()
    {
        var config = CreateValid();
        config.Capabilities.Git = false;
        config.GitProvider.Type = "invalid";
        config.GitProvider.SyncIntervalSeconds = 1;
        config.GitProvider.BranchTemplate = "no-key-here";
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_GitProvider_EmptyRepoList_Succeeds()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories = [];
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    // --- CredentialsRef validation (NEX-187) ---

    [Fact]
    public void Validate_CredentialsRefDocker_NoDockerCreds_Fails()
    {
        var config = CreateWithGitProvider();
        config.Docker.Credentials.Git.Token = "";
        config.Docker.Credentials.Git.SshKeyPath = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("no token or SSH key configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_UnknownCredentialsRef_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.CredentialsRef = "vault";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("not a recognized credentials source", result.FailureMessage);
    }

    [Fact]
    public void Validate_EmptyCredentialsRef_Fails()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.CredentialsRef = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:CredentialsRef is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_NullRepositories_Succeeds()
    {
        var config = CreateWithGitProvider();
        config.GitProvider.Repositories = null!;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    // --- CodebaseMemoryMcp validation (NEX-196) ---

    [Fact]
    public void Validate_McpEnabled_ValidConfig_Succeeds()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.Port = 3500;
        config.CodebaseMemoryMcp.HealthCheckIntervalSeconds = 30;
        config.CodebaseMemoryMcp.StartupTimeoutSeconds = 60;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_McpDisabled_SkipsValidation()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = false;
        config.CodebaseMemoryMcp.Port = 0; // Invalid but should be skipped
        config.CodebaseMemoryMcp.HealthCheckIntervalSeconds = 1;
        config.CodebaseMemoryMcp.StartupTimeoutSeconds = 1;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    [InlineData(1023)]
    public void Validate_McpPort_TooLow_Fails(int port)
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.Port = port;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:Port must be between 1024 and 65535", result.FailureMessage);
    }

    [Fact]
    public void Validate_McpPort_TooHigh_Fails()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.Port = 65536;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:Port must be between 1024 and 65535", result.FailureMessage);
    }

    [Fact]
    public void Validate_McpHealthCheckInterval_TooLow_Fails()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.HealthCheckIntervalSeconds = 5;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:HealthCheckIntervalSeconds must be at least 10", result.FailureMessage);
    }

    [Fact]
    public void Validate_McpStartupTimeout_TooLow_Fails()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.StartupTimeoutSeconds = 5;
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:StartupTimeoutSeconds must be at least 10", result.FailureMessage);
    }

    [Fact]
    public void Validate_McpPort_BoundaryValues_Succeeds()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.Port = 1024;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);

        config.CodebaseMemoryMcp.Port = 65535;
        result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_McpEnabled_EmptyNpxCommand_Fails()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.NpxCommand = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:NpxCommand is required when MCP is enabled", result.FailureMessage);
    }

    [Fact]
    public void Validate_McpEnabled_EmptyPackageName_Fails()
    {
        var config = CreateValid();
        config.CodebaseMemoryMcp.Enabled = true;
        config.CodebaseMemoryMcp.PackageName = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("CodebaseMemoryMcp:PackageName is required when MCP is enabled", result.FailureMessage);
    }
}
