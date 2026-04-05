using System.Text.Json;
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
    private readonly JobAssignHandler _sut;

    public JobAssignHandlerTests()
    {
        _projectManagerMock = new Mock<IProjectManager>();
        _jiraServiceMock = new Mock<IJiraService>();

        var config = new SpokeConfiguration
        {
            Capabilities = new SpokeConfiguration.CapabilitiesConfig { Jira = true }
        };

        _sut = new JobAssignHandler(
            _projectManagerMock.Object,
            _jiraServiceMock.Object,
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
}
