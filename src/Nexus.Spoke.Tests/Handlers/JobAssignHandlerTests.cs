using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Handlers;

public class JobAssignHandlerTests
{
    private readonly Mock<IProjectManager> _projectManagerMock;
    private readonly Mock<IJiraService> _jiraServiceMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IWorkerOutputStreamer> _outputStreamerMock;
    private readonly Mock<IJobLifecycleService> _lifecycleServiceMock;
    private readonly Mock<IJobArtifactService> _jobArtifactsMock;
    private readonly Mock<ISkillMerger> _skillMergerMock;
    private readonly Mock<IPromptAssembler> _promptAssemblerMock;
    private readonly ActiveJobTracker _activeJobTracker;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private readonly JobAssignHandler _sut;

    public JobAssignHandlerTests()
    {
        _projectManagerMock = new Mock<IProjectManager>();
        _jiraServiceMock = new Mock<IJiraService>();
        _dockerServiceMock = new Mock<IDockerService>();
        _outputStreamerMock = new Mock<IWorkerOutputStreamer>();
        _lifecycleServiceMock = new Mock<IJobLifecycleService>();
        _jobArtifactsMock = new Mock<IJobArtifactService>();
        _skillMergerMock = new Mock<ISkillMerger>();
        _skillMergerMock.Setup(m => m.MergeSkillsAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _promptAssemblerMock = new Mock<IPromptAssembler>();
        _activeJobTracker = new ActiveJobTracker();
        _appLifetimeMock = new Mock<IHostApplicationLifetime>();
        _appLifetimeMock.Setup(a => a.ApplicationStopping).Returns(CancellationToken.None);

        _promptAssemblerMock
            .Setup(m => m.AssembleAsync(It.IsAny<PromptAssemblyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("assembled prompt");

        var config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Jira = true }
        };

        _sut = new JobAssignHandler(
            _projectManagerMock.Object,
            _jiraServiceMock.Object,
            _dockerServiceMock.Object,
            _outputStreamerMock.Object,
            _lifecycleServiceMock.Object,
            _jobArtifactsMock.Object,
            _skillMergerMock.Object,
            _promptAssemblerMock.Object,
            _activeJobTracker,
            _appLifetimeMock.Object,
            Options.Create(config),
            NullLogger<JobAssignHandler>.Instance);
    }

    [Fact]
    public void CommandType_IsJobAssign()
    {
        Assert.Equal("job.assign", _sut.CommandType);
    }

    [Fact]
    public async Task HandleAsync_CreatesProjectForNewAssignment()
    {
        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "context",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-1" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-1"))
            .ReturnsAsync((ProjectInfo?)null);
        _projectManagerMock.Setup(m => m.CreateProjectAsync("TEST-1", "TEST-1", null))
            .ReturnsAsync(new ProjectInfo("TEST-1", "TEST-1", ProjectStatus.Planning,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-1"));

        var ticket = new TicketMetadata("TEST-1", "Summary", null, null, null, null, null);
        _jiraServiceMock.Setup(m => m.FetchTicketAsync("TEST-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await _sut.HandleAsync(command, CancellationToken.None);

        _projectManagerMock.Verify(m => m.CreateProjectAsync("TEST-1", "TEST-1", null), Times.Once);
        _jiraServiceMock.Verify(m => m.FetchTicketAsync("TEST-1", It.IsAny<CancellationToken>()), Times.Once);
        _projectManagerMock.Verify(m => m.SaveTicketMetadataAsync("TEST-1", ticket), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SkipsCreationForExistingProject()
    {
        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "context",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-1" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-1"))
            .ReturnsAsync(new ProjectInfo("TEST-1", "Test", ProjectStatus.Active,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-1"));

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await _sut.HandleAsync(command, CancellationToken.None);

        _projectManagerMock.Verify(m => m.CreateProjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SkipsJiraWhenDisabled()
    {
        var config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Jira = false }
        };
        var handler = new JobAssignHandler(
            _projectManagerMock.Object,
            _jiraServiceMock.Object,
            _dockerServiceMock.Object,
            _outputStreamerMock.Object,
            _lifecycleServiceMock.Object,
            _jobArtifactsMock.Object,
            _skillMergerMock.Object,
            _promptAssemblerMock.Object,
            _activeJobTracker,
            _appLifetimeMock.Object,
            Options.Create(config),
            NullLogger<JobAssignHandler>.Instance);

        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "context",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-1" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-1"))
            .ReturnsAsync((ProjectInfo?)null);
        _projectManagerMock.Setup(m => m.CreateProjectAsync("TEST-1", "TEST-1", null))
            .ReturnsAsync(new ProjectInfo("TEST-1", "TEST-1", ProjectStatus.Planning,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-1"));

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await handler.HandleAsync(command, CancellationToken.None);

        _jiraServiceMock.Verify(m => m.FetchTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HandlesJsonElementPayload()
    {
        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Test, "ctx",
            new JobParameters(null), false, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(assignment);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        _projectManagerMock.Setup(m => m.GetProjectAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProjectInfo("test", "test", ProjectStatus.Active,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null));

        var command = new CommandEnvelope("job.assign", element, DateTimeOffset.UtcNow);
        await _sut.HandleAsync(command, CancellationToken.None);

        // Should not throw — successfully deserialized from JsonElement
        _projectManagerMock.Verify(m => m.GetProjectAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReportsFailedWhenDockerDisabled()
    {
        // Default config has Docker = false
        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "context",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-1" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-1"))
            .ReturnsAsync(new ProjectInfo("TEST-1", "Test", ProjectStatus.Active,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-1"));

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await _sut.HandleAsync(command, CancellationToken.None);

        _lifecycleServiceMock.Verify(m => m.ReportStatusAsync(
            assignment.JobId, assignment.ProjectId, "TEST-1",
            JobStatus.Queued, JobStatus.Failed,
            It.Is<string>(s => s.Contains("Docker")),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RejectsWhenAtMaxConcurrency()
    {
        var config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Docker = true },
            Approval = new SpokeConfiguration.ApprovalConfig { MaxConcurrentJobs = 1 }
        };
        var handler = new JobAssignHandler(
            _projectManagerMock.Object,
            _jiraServiceMock.Object,
            _dockerServiceMock.Object,
            _outputStreamerMock.Object,
            _lifecycleServiceMock.Object,
            _jobArtifactsMock.Object,
            _skillMergerMock.Object,
            _promptAssemblerMock.Object,
            _activeJobTracker,
            _appLifetimeMock.Object,
            Options.Create(config),
            NullLogger<JobAssignHandler>.Instance);

        // Fill up the tracker
        using var existingCts = new CancellationTokenSource();
        _activeJobTracker.TryAdd(Guid.NewGuid(), new ActiveJob(
            Guid.NewGuid(), Guid.NewGuid(), "existing",
            "container-1", existingCts, DateTimeOffset.UtcNow));

        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "context",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-2" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-2"))
            .ReturnsAsync(new ProjectInfo("TEST-2", "Test", ProjectStatus.Active,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-2"));

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await handler.HandleAsync(command, CancellationToken.None);

        // Should not attempt to launch
        _dockerServiceMock.Verify(m => m.EnsureImageAsync(It.IsAny<CancellationToken>()), Times.Never);

        // Should report capacity rejection to hub
        _lifecycleServiceMock.Verify(m => m.ReportStatusAsync(
            assignment.JobId, assignment.ProjectId, "TEST-2",
            JobStatus.Queued, JobStatus.Failed,
            It.Is<string>(s => s.Contains("capacity")),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CallsPromptAssembler()
    {
        var config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Docker = true },
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = Path.GetTempPath() }
        };
        var handler = new JobAssignHandler(
            _projectManagerMock.Object,
            _jiraServiceMock.Object,
            _dockerServiceMock.Object,
            _outputStreamerMock.Object,
            _lifecycleServiceMock.Object,
            _jobArtifactsMock.Object,
            _skillMergerMock.Object,
            _promptAssemblerMock.Object,
            _activeJobTracker,
            _appLifetimeMock.Object,
            Options.Create(config),
            NullLogger<JobAssignHandler>.Instance);

        var assignment = new JobAssignment(
            Guid.NewGuid(), Guid.NewGuid(), JobType.Implement, "build it",
            new JobParameters(new Dictionary<string, object> { ["projectKey"] = "TEST-3" }),
            false, DateTimeOffset.UtcNow);

        _projectManagerMock.Setup(m => m.GetProjectAsync("TEST-3"))
            .ReturnsAsync(new ProjectInfo("TEST-3", "Test", ProjectStatus.Active,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "TEST-3"));
        _projectManagerMock.Setup(m => m.GetProjectPath("TEST-3"))
            .Returns(Path.Combine(Path.GetTempPath(), "projects", "TEST-3"));
        _projectManagerMock.Setup(m => m.GetMetaPath("TEST-3"))
            .Returns(Path.Combine(Path.GetTempPath(), "projects", "TEST-3", ".meta"));

        _jobArtifactsMock.Setup(m => m.InitializeJobAsync("TEST-3", assignment.JobId))
            .ReturnsAsync(Path.Combine(Path.GetTempPath(), "projects", "TEST-3", "jobs", $"job-{assignment.JobId}"));

        _dockerServiceMock.Setup(m => m.LaunchWorkerAsync(It.IsAny<WorkerLaunchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-abc");

        var command = new CommandEnvelope("job.assign", assignment, DateTimeOffset.UtcNow);
        await handler.HandleAsync(command, CancellationToken.None);

        _promptAssemblerMock.Verify(
            m => m.AssembleAsync(
                It.Is<PromptAssemblyContext>(ctx =>
                    ctx.JobId == assignment.JobId &&
                    ctx.ProjectKey == "TEST-3" &&
                    ctx.HubContext == "build it"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
