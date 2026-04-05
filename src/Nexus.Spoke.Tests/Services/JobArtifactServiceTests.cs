using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class JobArtifactServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JobArtifactService _sut;
    private readonly ProjectManager _projectManager;

    public JobArtifactServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "projects"));

        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };

        _projectManager = new ProjectManager(
            Options.Create(config),
            new Mock<IHubConnectionService>().Object,
            NullLogger<ProjectManager>.Instance);

        _sut = new JobArtifactService(
            _projectManager,
            NullLogger<JobArtifactService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task InitializeJobAsync_CreatesDirectory()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var jobId = Guid.NewGuid();

        var jobDir = await _sut.InitializeJobAsync("TEST-1", jobId);

        Assert.True(Directory.Exists(jobDir));
        Assert.True(File.Exists(Path.Combine(jobDir, "status.json")));
    }

    [Fact]
    public async Task AppendOutputAsync_AccumulatesContent()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var jobId = Guid.NewGuid();
        await _sut.InitializeJobAsync("TEST-1", jobId);

        await _sut.AppendOutputAsync("TEST-1", jobId, "line 1\n");
        await _sut.AppendOutputAsync("TEST-1", jobId, "line 2\n");

        var outputPath = Path.Combine(_projectManager.GetProjectPath("TEST-1"), "jobs", $"job-{jobId}", "output.log");
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("line 1", content);
        Assert.Contains("line 2", content);
    }

    [Fact]
    public async Task WritePromptAsync_WritesFile()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var jobId = Guid.NewGuid();
        await _sut.InitializeJobAsync("TEST-1", jobId);

        await _sut.WritePromptAsync("TEST-1", jobId, "# Implement feature X");

        var promptPath = Path.Combine(_projectManager.GetProjectPath("TEST-1"), "jobs", $"job-{jobId}", "prompt.md");
        var content = await File.ReadAllTextAsync(promptPath);
        Assert.Equal("# Implement feature X", content);
    }

    [Fact]
    public async Task WriteSummaryAsync_WritesFile()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var jobId = Guid.NewGuid();
        await _sut.InitializeJobAsync("TEST-1", jobId);

        await _sut.WriteSummaryAsync("TEST-1", jobId, "Job completed successfully.");

        var artifact = await _sut.GetJobAsync("TEST-1", jobId);
        Assert.Equal("Job completed successfully.", artifact!.Summary);
    }

    [Fact]
    public async Task WriteStatusAsync_UpdatesStatus()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var jobId = Guid.NewGuid();
        await _sut.InitializeJobAsync("TEST-1", jobId);

        await _sut.WriteStatusAsync("TEST-1", jobId, JobStatus.Running);
        var artifact = await _sut.GetJobAsync("TEST-1", jobId);
        Assert.Equal(JobStatus.Running, artifact!.Status);

        await _sut.WriteStatusAsync("TEST-1", jobId, JobStatus.Completed);
        artifact = await _sut.GetJobAsync("TEST-1", jobId);
        Assert.Equal(JobStatus.Completed, artifact!.Status);
        Assert.NotNull(artifact.CompletedAt);
    }

    [Fact]
    public async Task GetJobAsync_ReturnsNullForMissing()
    {
        var result = await _sut.GetJobAsync("TEST-1", Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task ListJobsAsync_ReturnsAllJobs()
    {
        await _projectManager.CreateProjectAsync("TEST-1");
        var job1 = Guid.NewGuid();
        var job2 = Guid.NewGuid();
        await _sut.InitializeJobAsync("TEST-1", job1);
        await _sut.InitializeJobAsync("TEST-1", job2);

        var list = await _sut.ListJobsAsync("TEST-1");

        Assert.Equal(2, list.Count);
    }
}
