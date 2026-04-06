using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IJobArtifactService
{
    Task<string> InitializeJobAsync(string projectKey, Guid jobId);
    Task AppendOutputAsync(string projectKey, Guid jobId, string content);
    Task WritePromptAsync(string projectKey, Guid jobId, string prompt);
    Task WriteSummaryAsync(string projectKey, Guid jobId, string summary);
    Task WriteStatusAsync(string projectKey, Guid jobId, JobStatus status, Dictionary<string, object>? metrics = null);
    Task<JobArtifact?> GetJobAsync(string projectKey, Guid jobId);
    Task<IReadOnlyList<JobArtifact>> ListJobsAsync(string projectKey);
}
