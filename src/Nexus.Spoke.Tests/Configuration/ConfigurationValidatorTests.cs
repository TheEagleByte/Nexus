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
}
