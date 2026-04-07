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

    [Fact]
    public void Validate_GitEnabled_ValidConfig_Succeeds()
    {
        var config = CreateValid();
        config.Capabilities.Git = true;
        config.Git = new SpokeConfiguration.GitConfig
        {
            UserName = "Nexus",
            UserEmail = "nexus@test.local",
            TimeoutSeconds = 60
        };
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_GitEnabled_MissingUserName_Fails()
    {
        var config = CreateValid();
        config.Capabilities.Git = true;
        config.Git = new SpokeConfiguration.GitConfig { UserName = "", UserEmail = "a@b.c" };
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Git:UserName is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitEnabled_MissingUserEmail_Fails()
    {
        var config = CreateValid();
        config.Capabilities.Git = true;
        config.Git = new SpokeConfiguration.GitConfig { UserName = "Test", UserEmail = "" };
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Git:UserEmail is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitEnabled_MissingDefaultBranch_Fails()
    {
        var config = CreateValid();
        config.Capabilities.Git = true;
        config.Git = new SpokeConfiguration.GitConfig { DefaultBranch = "" };
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Git:DefaultBranch is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitEnabled_TimeoutTooLow_Fails()
    {
        var config = CreateValid();
        config.Capabilities.Git = true;
        config.Git = new SpokeConfiguration.GitConfig { TimeoutSeconds = 5 };
        var result = _validator.Validate(null, config);
        Assert.True(result.Failed);
        Assert.Contains("Git:TimeoutSeconds must be at least 10", result.FailureMessage);
    }

    [Fact]
    public void Validate_GitDisabled_SkipsGitValidation()
    {
        var config = CreateValid();
        config.Capabilities.Git = false;
        config.Git = new SpokeConfiguration.GitConfig { UserName = "", UserEmail = "", TimeoutSeconds = 1 };
        var result = _validator.Validate(null, config);
        Assert.True(result.Succeeded);
    }
}
