using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class ProjectManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IHubConnectionService> _hubMock;
    private readonly ProjectManager _sut;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProjectManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "projects"));

        _hubMock = new Mock<IHubConnectionService>();
        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };

        _sut = new ProjectManager(
            Options.Create(config),
            _hubMock.Object,
            NullLogger<ProjectManager>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task CreateProjectAsync_CreatesCorrectDirectoryStructure()
    {
        var result = await _sut.CreateProjectAsync("TEST-1", "Test Project", "A test");

        Assert.Equal("TEST-1", result.ProjectKey);
        Assert.Equal("Test Project", result.Name);
        Assert.Equal(ProjectStatus.Planning, result.Status);
        Assert.True(Directory.Exists(_sut.GetProjectPath("TEST-1")));
        Assert.True(Directory.Exists(_sut.GetMetaPath("TEST-1")));
        Assert.True(Directory.Exists(Path.Combine(_sut.GetProjectPath("TEST-1"), "jobs")));
        Assert.True(File.Exists(Path.Combine(_sut.GetMetaPath("TEST-1"), "status.json")));
        Assert.True(File.Exists(Path.Combine(_sut.GetMetaPath("TEST-1"), "context.md")));
        Assert.True(File.Exists(Path.Combine(_sut.GetMetaPath("TEST-1"), "plan.md")));
    }

    [Fact]
    public async Task CreateProjectAsync_IsIdempotent()
    {
        await _sut.CreateProjectAsync("TEST-1", "First");
        var second = await _sut.CreateProjectAsync("TEST-1", "Second");

        Assert.Equal("TEST-1", second.ProjectKey);
        Assert.Equal(ProjectStatus.Planning, second.Status);
    }

    [Fact]
    public async Task GetProjectAsync_ReturnsNullForMissing()
    {
        var result = await _sut.GetProjectAsync("NOPE-1");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProjectAsync_ReturnsPopulatedProjectInfo()
    {
        await _sut.CreateProjectAsync("TEST-1", "Test Project");

        var ticket = new TicketMetadata("TEST-1", "Ticket Summary", "Full description", null, "Task", null, null);
        await _sut.SaveTicketMetadataAsync("TEST-1", ticket);

        var result = await _sut.GetProjectAsync("TEST-1");

        Assert.NotNull(result);
        Assert.Equal("Ticket Summary", result.Name);
        Assert.Equal(ProjectStatus.Planning, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransitionSucceeds()
    {
        await _sut.CreateProjectAsync("TEST-1");

        await _sut.UpdateStatusAsync("TEST-1", ProjectStatus.Active);

        var project = await _sut.GetProjectAsync("TEST-1");
        Assert.Equal(ProjectStatus.Active, project!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransitionThrows()
    {
        await _sut.CreateProjectAsync("TEST-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync("TEST-1", ProjectStatus.Completed));
    }

    [Fact]
    public async Task UpdateStatusAsync_AppendsHistory()
    {
        await _sut.CreateProjectAsync("TEST-1");
        await _sut.UpdateStatusAsync("TEST-1", ProjectStatus.Active);

        var statusJson = await File.ReadAllTextAsync(Path.Combine(_sut.GetMetaPath("TEST-1"), "status.json"));
        var status = JsonSerializer.Deserialize<StatusMetadata>(statusJson, JsonOptions);

        Assert.Single(status!.History);
        Assert.Equal(ProjectStatus.Planning, status.History[0].From);
        Assert.Equal(ProjectStatus.Active, status.History[0].To);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonexistentProjectThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync("NOPE-1", ProjectStatus.Active));
    }

    [Fact]
    public async Task ListProjectsAsync_ReturnsAllProjects()
    {
        await _sut.CreateProjectAsync("TEST-1");
        await _sut.CreateProjectAsync("TEST-2");

        var list = await _sut.ListProjectsAsync();

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task ListProjectsAsync_FiltersbyStatus()
    {
        await _sut.CreateProjectAsync("TEST-1");
        await _sut.CreateProjectAsync("TEST-2");
        await _sut.UpdateStatusAsync("TEST-1", ProjectStatus.Active);

        var active = await _sut.ListProjectsAsync(ProjectStatus.Active);
        var planning = await _sut.ListProjectsAsync(ProjectStatus.Planning);

        Assert.Single(active);
        Assert.Equal("TEST-1", active[0].ProjectKey);
        Assert.Single(planning);
        Assert.Equal("TEST-2", planning[0].ProjectKey);
    }

    [Fact]
    public async Task SaveTicketMetadataAsync_PersistsToDisk()
    {
        await _sut.CreateProjectAsync("TEST-1");
        var ticket = new TicketMetadata("TEST-1", "Summary", "Desc", ["AC1", "AC2"], "Bug", ["label1"], "John");

        await _sut.SaveTicketMetadataAsync("TEST-1", ticket);

        var path = Path.Combine(_sut.GetMetaPath("TEST-1"), "ticket.json");
        Assert.True(File.Exists(path));
        var json = await File.ReadAllTextAsync(path);
        var loaded = JsonSerializer.Deserialize<TicketMetadata>(json, JsonOptions);
        Assert.Equal("Summary", loaded!.Summary);
        Assert.Equal(2, loaded.AcceptanceCriteria!.Length);
    }
}
