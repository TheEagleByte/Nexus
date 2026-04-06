using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class DockerServiceTests
{
    private static SpokeConfiguration CreateConfig(string image = "nexus-worker:latest") =>
        new()
        {
            Docker = new SpokeConfiguration.DockerConfig
            {
                WorkerImage = image,
                WorkerDockerfilePath = "worker/Dockerfile",
                ResourceLimits = new SpokeConfiguration.DockerResourceLimitsConfig
                {
                    MemoryBytes = 8_589_934_592,
                    CpuCount = 2,
                    DiskLimitBytes = 107_374_182_400
                },
                NetworkMode = "none",
                ReadOnlyRootFs = true,
                ContainerUser = "1000:1000",
                TimeoutSeconds = 14400
            }
        };

    [Fact]
    public async Task Constructor_CreatesServiceWithoutThrow()
    {
        // DockerService connects to the local Docker daemon.
        // This test verifies it can be constructed (doesn't throw on init).
        // Actual Docker operations require a running Docker daemon.
        var config = CreateConfig();
        await using var service = new DockerService(Options.Create(config), NullLogger<DockerService>.Instance);
        Assert.NotNull(service);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var config = CreateConfig();
        var service = new DockerService(Options.Create(config), NullLogger<DockerService>.Instance);

        await service.DisposeAsync();
        await service.DisposeAsync(); // Should not throw
    }

    [Fact]
    public void WorkerLaunchRequest_RecordProperties()
    {
        var jobId = Guid.NewGuid();
        var request = new WorkerLaunchRequest(
            jobId, "TEST-1", JobType.Implement,
            "/tmp/prompt.md", "/repo", "/output",
            "/skills/spoke", "/skills/project", "/tmp/merged-skills.md");

        Assert.Equal(jobId, request.JobId);
        Assert.Equal("TEST-1", request.ProjectKey);
        Assert.Equal(JobType.Implement, request.JobType);
        Assert.Equal("/tmp/prompt.md", request.PromptFilePath);
        Assert.Equal("/repo", request.RepoPath);
        Assert.Equal("/output", request.OutputPath);
        Assert.Equal("/skills/spoke", request.SpokeSkillsPath);
        Assert.Equal("/skills/project", request.ProjectSkillsPath);
        Assert.Equal("/tmp/merged-skills.md", request.MergedSkillsFilePath);
    }

    [Fact]
    public void WorkerLaunchRequest_AllowsNullSkillsPaths()
    {
        var request = new WorkerLaunchRequest(
            Guid.NewGuid(), "TEST-1", JobType.Test,
            "/tmp/prompt.md", "/repo", "/output",
            null, null, null);

        Assert.Null(request.SpokeSkillsPath);
        Assert.Null(request.ProjectSkillsPath);
        Assert.Null(request.MergedSkillsFilePath);
    }
}
