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

    private static readonly object[] OutputLockStripes = CreateLockStripes(64);

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

        lock (OutputLockStripes[(jobId.GetHashCode() & int.MaxValue) % OutputLockStripes.Length])
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
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    await File.ReadAllTextAsync(statusPath));
                if (existing?.TryGetValue("createdAt", out var createdAt) == true &&
                    createdAt.ValueKind == JsonValueKind.String &&
                    createdAt.GetString() is { } createdAtValue)
                    statusData["createdAt"] = createdAtValue;
            }
            catch (JsonException)
            {
                // Corrupt existing status.json; continue with fresh status payload.
            }
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

        Dictionary<string, JsonElement>? data;
        try
        {
            var json = await File.ReadAllTextAsync(statusPath);
            data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (data is null) return null;

        if (!data.TryGetValue("jobId", out var id) ||
            id.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(id.GetString(), out var jobId))
            return null;
        if (!data.TryGetValue("createdAt", out var c) ||
            c.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(c.GetString(), out var createdAt))
            return null;
        var status = data.TryGetValue("status", out var s) &&
            s.ValueKind == JsonValueKind.String &&
            Enum.TryParse<JobStatus>(s.GetString(), ignoreCase: true, out var parsedStatus)
            ? parsedStatus : JobStatus.Queued;
        var completedAt = data.TryGetValue("completedAt", out var comp) &&
            comp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(comp.GetString(), out var parsedCompleted)
            ? parsedCompleted : (DateTimeOffset?)null;

        string? summary = null;
        var summaryPath = Path.Combine(jobDir, "summary.md");
        if (File.Exists(summaryPath))
            summary = await File.ReadAllTextAsync(summaryPath);

        return new JobArtifact(jobId, projectKey, status, createdAt, completedAt, summary);
    }

    private string GetJobDirectory(string projectKey, Guid jobId) =>
        Path.Combine(projectManager.GetProjectPath(projectKey), "jobs", $"job-{jobId}");

    private static object[] CreateLockStripes(int size)
    {
        var stripes = new object[size];
        for (var i = 0; i < size; i++)
            stripes[i] = new object();
        return stripes;
    }
}
