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

    // --- GitProvider validation tests ---

    private static SpokeConfiguration CreateWithGitProvider(
        string type = "github",
        string credentialsRef = "docker",
        SpokeConfiguration.RepositoryConfig[]? repos = null)
    {
        var config = CreateWithDockerAndNetwork("token");
        config.GitProvider = new SpokeConfiguration.GitProviderConfig
        {
            Type = type,
            CredentialsRef = credentialsRef,
            Repositories = repos ?? []
        };
        return config;
    }

    [Fact]
    public void Validate_NoGitProvider_Succeeds()
    {
        var config = CreateValid();
        config.GitProvider = null;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidGitHubProvider_Succeeds()
    {
        var config = CreateWithGitProvider("github");
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidGitLabProvider_Succeeds()
    {
        var config = CreateWithGitProvider("gitlab");
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidProviderType_Fails()
    {
        var config = CreateWithGitProvider("bitbucket");
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:Type must be 'github' or 'gitlab'", result.FailureMessage);
    }

    [Fact]
    public void Validate_CredentialsRefDocker_NoDockerCreds_Fails()
    {
        var config = CreateWithGitProvider("github", "docker");
        config.Docker.Credentials.Git.Token = "";
        config.Docker.Credentials.Git.SshKeyPath = "";
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("no token or SSH key configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_UnknownCredentialsRef_Fails()
    {
        var config = CreateWithGitProvider("github", "vault");
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("not a recognized credentials source", result.FailureMessage);
    }

    [Fact]
    public void Validate_EmptyRepositories_Succeeds()
    {
        var config = CreateWithGitProvider("github", "docker", []);
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RepositoryMissingName_Fails()
    {
        var repos = new[]
        {
            new SpokeConfiguration.RepositoryConfig
            {
                Name = "",
                RemoteUrl = "git@github.com:org/repo.git"
            }
        };
        var config = CreateWithGitProvider("github", "docker", repos);
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Repositories[0]:Name is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_RepositoryMissingRemoteUrl_Fails()
    {
        var repos = new[]
        {
            new SpokeConfiguration.RepositoryConfig
            {
                Name = "my-repo",
                RemoteUrl = ""
            }
        };
        var config = CreateWithGitProvider("github", "docker", repos);
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Repositories[0]:RemoteUrl is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_MultipleRepositories_AllValid_Succeeds()
    {
        var repos = new[]
        {
            new SpokeConfiguration.RepositoryConfig
            {
                Name = "app",
                RemoteUrl = "git@github.com:org/app.git",
                DefaultBranch = "main"
            },
            new SpokeConfiguration.RepositoryConfig
            {
                Name = "lib",
                RemoteUrl = "git@github.com:org/lib.git"
            }
        };
        var config = CreateWithGitProvider("github", "docker", repos);
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EmptyCredentialsRef_Fails()
    {
        var config = CreateWithGitProvider("github", "");
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("GitProvider:CredentialsRef is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_NullRepositories_Succeeds()
    {
        var config = CreateWithGitProvider("github", "docker");
        config.GitProvider!.Repositories = null!;
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }
}
