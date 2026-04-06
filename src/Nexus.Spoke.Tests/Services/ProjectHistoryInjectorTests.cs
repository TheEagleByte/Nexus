using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class ProjectHistoryInjectorTests
{
    private readonly Mock<IJobArtifactService> _jobArtifactsMock;
    private readonly ProjectHistoryInjector _sut;

    public ProjectHistoryInjectorTests()
    {
        _jobArtifactsMock = new Mock<IJobArtifactService>();
        _sut = new ProjectHistoryInjector(
            _jobArtifactsMock.Object,
            NullLogger<ProjectHistoryInjector>.Instance);
    }

    [Fact]
    public async Task GetHistorySummary_WithCompletedJobs_ReturnsSummaries()
    {
        var currentJobId = Guid.NewGuid();
        var completedJobId = Guid.NewGuid();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(new List<JobArtifact>
            {
                new(completedJobId, "proj-1", JobStatus.Completed,
                    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, "Fixed the login bug")
            });

        var result = await _sut.GetHistorySummaryAsync("proj-1", currentJobId);

        Assert.Contains("Fixed the login bug", result);
        Assert.Contains(completedJobId.ToString()[..8], result);
    }

    [Fact]
    public async Task GetHistorySummary_ExcludesCurrentJob()
    {
        var currentJobId = Guid.NewGuid();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(new List<JobArtifact>
            {
                new(currentJobId, "proj-1", JobStatus.Completed,
                    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, "Should be excluded")
            });

        var result = await _sut.GetHistorySummaryAsync("proj-1", currentJobId);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetHistorySummary_ExcludesNonCompletedJobs()
    {
        var currentJobId = Guid.NewGuid();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(new List<JobArtifact>
            {
                new(Guid.NewGuid(), "proj-1", JobStatus.Running,
                    DateTimeOffset.UtcNow, null, "Still running"),
                new(Guid.NewGuid(), "proj-1", JobStatus.Failed,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Failed job"),
                new(Guid.NewGuid(), "proj-1", JobStatus.Queued,
                    DateTimeOffset.UtcNow, null, "Queued job")
            });

        var result = await _sut.GetHistorySummaryAsync("proj-1", currentJobId);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetHistorySummary_ExcludesJobsWithNullSummary()
    {
        var currentJobId = Guid.NewGuid();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(new List<JobArtifact>
            {
                new(Guid.NewGuid(), "proj-1", JobStatus.Completed,
                    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, null)
            });

        var result = await _sut.GetHistorySummaryAsync("proj-1", currentJobId);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetHistorySummary_WithNoJobs_ReturnsEmpty()
    {
        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(new List<JobArtifact>());

        var result = await _sut.GetHistorySummaryAsync("proj-1", Guid.NewGuid());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetHistorySummary_RespectsMaxEntries()
    {
        var jobs = Enumerable.Range(0, 10)
            .Select(i => new JobArtifact(
                Guid.NewGuid(), "proj-1", JobStatus.Completed,
                DateTimeOffset.UtcNow.AddDays(-i), DateTimeOffset.UtcNow.AddDays(-i + 1),
                $"Summary for job {i}"))
            .ToList();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(jobs);

        var result = await _sut.GetHistorySummaryAsync("proj-1", Guid.NewGuid(), maxEntries: 3);

        // Should only include 3 entries
        var headerCount = result.Split("### Job").Length - 1;
        Assert.Equal(3, headerCount);
    }

    [Fact]
    public async Task GetHistorySummary_TruncatesToBudget()
    {
        var jobs = Enumerable.Range(0, 5)
            .Select(i => new JobArtifact(
                Guid.NewGuid(), "proj-1", JobStatus.Completed,
                DateTimeOffset.UtcNow.AddDays(-i), DateTimeOffset.UtcNow.AddDays(-i + 1),
                new string('A', 1000)))
            .ToList();

        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ReturnsAsync(jobs);

        var result = await _sut.GetHistorySummaryAsync("proj-1", Guid.NewGuid(), maxTotalChars: 500);

        Assert.False(string.IsNullOrWhiteSpace(result), "Result should not be empty when truncating");
        Assert.True(result.Length <= 500, $"Result should be under 500 chars, was {result.Length}");
    }

    [Fact]
    public async Task GetHistorySummary_HandlesListJobsFailure()
    {
        _jobArtifactsMock.Setup(m => m.ListJobsAsync("proj-1"))
            .ThrowsAsync(new IOException("disk error"));

        var result = await _sut.GetHistorySummaryAsync("proj-1", Guid.NewGuid());

        Assert.Equal(string.Empty, result);
    }
}
