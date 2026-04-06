using System.Collections.Concurrent;
using System.Text.Json;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class JobArtifactService(
    IProjectManager projectManager,
    ILogger<JobArtifactService> logger) : IJobArtifactService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, object> _outputLocks = new();

    public Task<string> InitializeJobAsync(string projectKey, Guid jobId)
    {
        var jobDir = GetJobDirectory(projectKey, jobId);
        Directory.CreateDirectory(jobDir);

        var statusPath = Path.Combine(jobDir, "status.json");
        var statusData = new Dictionary<string, object>
        {
            ["status"] = JobStatus.Queued.ToString(),
            ["createdAt"] = DateTimeOffset.UtcNow,
            ["jobId"] = jobId.ToString()
        };
        File.WriteAllText(statusPath, JsonSerializer.Serialize(statusData, JsonOptions));

        logger.LogInformation("Initialized job artifacts for {ProjectKey}/job-{JobId}", projectKey, jobId);
        return Task.FromResult(jobDir);
    }

    public Task AppendOutputAsync(string projectKey, Guid jobId, string content)
    {
        var outputPath = Path.Combine(GetJobDirectory(projectKey, jobId), "output.log");

        lock (_outputLocks.GetOrAdd(jobId, _ => new object()))
        {
            File.AppendAllText(outputPath, content);
        }

        return Task.CompletedTask;
    }

    public async Task WritePromptAsync(string projectKey, Guid jobId, string prompt)
    {
        var promptPath = Path.Combine(GetJobDirectory(projectKey, jobId), "prompt.md");
        await File.WriteAllTextAsync(promptPath, prompt);
    }

    public async Task WriteSummaryAsync(string projectKey, Guid jobId, string summary)
    {
        var summaryPath = Path.Combine(GetJobDirectory(projectKey, jobId), "summary.md");
        await File.WriteAllTextAsync(summaryPath, summary);
    }

    public async Task WriteStatusAsync(string projectKey, Guid jobId, JobStatus status, Dictionary<string, object>? metrics = null)
    {
        var statusPath = Path.Combine(GetJobDirectory(projectKey, jobId), "status.json");

        var statusData = new Dictionary<string, object>
        {
            ["status"] = status.ToString(),
            ["jobId"] = jobId.ToString(),
            ["updatedAt"] = DateTimeOffset.UtcNow
        };

        if (status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
            statusData["completedAt"] = DateTimeOffset.UtcNow;

        // Preserve createdAt from existing status
        if (File.Exists(statusPath))
        {
            var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                await File.ReadAllTextAsync(statusPath));
            if (existing?.TryGetValue("createdAt", out var createdAt) == true)
                statusData["createdAt"] = createdAt.GetString()!;
        }

        if (metrics is not null)
            statusData["metrics"] = metrics;

        var json = JsonSerializer.Serialize(statusData, JsonOptions);
        await File.WriteAllTextAsync(statusPath, json);

        logger.LogDebug("Job {JobId} status updated to {Status}", jobId, status);
    }

    public async Task<JobArtifact?> GetJobAsync(string projectKey, Guid jobId)
    {
        var jobDir = GetJobDirectory(projectKey, jobId);
        if (!Directory.Exists(jobDir)) return null;

        return await ReadJobArtifactAsync(projectKey, jobDir);
    }

    public async Task<IReadOnlyList<JobArtifact>> ListJobsAsync(string projectKey)
    {
        var jobsDir = Path.Combine(projectManager.GetProjectPath(projectKey), "jobs");
        if (!Directory.Exists(jobsDir)) return [];

        var artifacts = new List<JobArtifact>();
        foreach (var dir in Directory.GetDirectories(jobsDir, "job-*"))
        {
            var artifact = await ReadJobArtifactAsync(projectKey, dir);
            if (artifact is not null)
                artifacts.Add(artifact);
        }

        return artifacts.OrderByDescending(a => a.CreatedAt).ToList();
    }

    private static async Task<JobArtifact?> ReadJobArtifactAsync(string projectKey, string jobDir)
    {
        var statusPath = Path.Combine(jobDir, "status.json");
        if (!File.Exists(statusPath)) return null;

        var json = await File.ReadAllTextAsync(statusPath);
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data is null) return null;

        var jobId = data.TryGetValue("jobId", out var id) && Guid.TryParse(id.GetString(), out var parsedId)
            ? parsedId : Guid.Empty;
        var status = data.TryGetValue("status", out var s) && Enum.TryParse<JobStatus>(s.GetString(), ignoreCase: true, out var parsedStatus)
            ? parsedStatus : JobStatus.Queued;
        var createdAt = data.TryGetValue("createdAt", out var c) && DateTimeOffset.TryParse(c.GetString(), out var parsedCreated)
            ? parsedCreated : DateTimeOffset.MinValue;
        var completedAt = data.TryGetValue("completedAt", out var comp) && DateTimeOffset.TryParse(comp.GetString(), out var parsedCompleted)
            ? parsedCompleted : (DateTimeOffset?)null;

        string? summary = null;
        var summaryPath = Path.Combine(jobDir, "summary.md");
        if (File.Exists(summaryPath))
            summary = await File.ReadAllTextAsync(summaryPath);

        return new JobArtifact(jobId, projectKey, status, createdAt, completedAt, summary);
    }

    private string GetJobDirectory(string projectKey, Guid jobId) =>
        Path.Combine(projectManager.GetProjectPath(projectKey), "jobs", $"job-{jobId}");
}
