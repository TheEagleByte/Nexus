using Microsoft.Extensions.Configuration;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Tests.Configuration;

public class SpokeConfigurationTests
{
    [Fact]
    public void BindsFromInMemoryConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Spoke:Id"] = "spoke-alpha-001",
                ["Spoke:Name"] = "Work Laptop",
                ["Spoke:Os"] = "windows",
                ["Spoke:Architecture"] = "x64",
                ["Hub:Url"] = "wss://hub.test/api/hub",
                ["Hub:Token"] = "secret-token",
                ["Capabilities:Jira"] = "true",
                ["Capabilities:Git"] = "true",
                ["Capabilities:Docker"] = "false",
                ["Workspace:BaseDirectory"] = "/tmp/nexus-test",
                ["Approval:Mode"] = "full_autonomy",
                ["Approval:MaxConcurrentJobs"] = "3",
                ["Approval:HeartbeatIntervalSeconds"] = "15"
            })
            .Build();

        var spokeConfig = new SpokeConfiguration();
        config.Bind(spokeConfig);

        Assert.Equal("spoke-alpha-001", spokeConfig.Spoke.Id);
        Assert.Equal("Work Laptop", spokeConfig.Spoke.Name);
        Assert.Equal("windows", spokeConfig.Spoke.Os);
        Assert.Equal("x64", spokeConfig.Spoke.Architecture);
        Assert.Equal("wss://hub.test/api/hub", spokeConfig.Hub.Url);
        Assert.Equal("secret-token", spokeConfig.Hub.Token);
        Assert.True(spokeConfig.Capabilities.Jira);
        Assert.True(spokeConfig.Capabilities.Git);
        Assert.False(spokeConfig.Capabilities.Docker);
        Assert.Equal("/tmp/nexus-test", spokeConfig.Workspace.BaseDirectory);
        Assert.Equal("full_autonomy", spokeConfig.Approval.Mode);
        Assert.Equal(3, spokeConfig.Approval.MaxConcurrentJobs);
        Assert.Equal(15, spokeConfig.Approval.HeartbeatIntervalSeconds);
    }

    [Fact]
    public void EnvironmentVariableOverridesConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Spoke:Id"] = "original-id",
                ["Spoke:Name"] = "Original Name",
                ["Hub:Url"] = "wss://original/api/hub",
                ["Hub:Token"] = "original-token"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Spoke:Id"] = "overridden-id"
            })
            .Build();

        var spokeConfig = new SpokeConfiguration();
        config.Bind(spokeConfig);

        Assert.Equal("overridden-id", spokeConfig.Spoke.Id);
        Assert.Equal("Original Name", spokeConfig.Spoke.Name);
    }

    [Fact]
    public void DefaultValues_AreReasonable()
    {
        var config = new SpokeConfiguration();

        Assert.Equal("plan_review", config.Approval.Mode);
        Assert.Equal(5, config.Approval.BatchSize);
        Assert.Equal(1, config.Approval.MaxConcurrentJobs);
        Assert.Equal(30, config.Approval.HeartbeatIntervalSeconds);
    }
}
