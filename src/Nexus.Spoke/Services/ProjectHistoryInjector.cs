using System.Text;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class ProjectHistoryInjector(
    IJobArtifactService jobArtifacts,
    ILogger<ProjectHistoryInjector> logger) : IProjectHistoryInjector
{
    public async Task<string> GetHistorySummaryAsync(
        string projectKey,
        Guid currentJobId,
        int maxEntries = 5,
        int maxTotalChars = 3000,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobArtifact> jobs;
        try
        {
            jobs = await jobArtifacts.ListJobsAsync(projectKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list jobs for project {ProjectKey}", projectKey);
            return string.Empty;
        }

        var relevant = jobs
            .Where(j => j.JobId != currentJobId
                        && j.Status == JobStatus.Completed
                        && !string.IsNullOrWhiteSpace(j.Summary))
            .Take(maxEntries)
            .ToList();

        if (relevant.Count == 0)
            return string.Empty;

        var charBudgetPerEntry = maxTotalChars / relevant.Count;
        var sb = new StringBuilder();

        foreach (var job in relevant)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var shortId = job.JobId.ToString()[..8];
            var date = job.CompletedAt?.ToString("yyyy-MM-dd") ?? "unknown";
            var summary = TruncateSummary(job.Summary!, charBudgetPerEntry);

            var entry = $"### Job {shortId} ({date})\n{summary}\n\n";

            if (sb.Length + entry.Length > maxTotalChars)
                break;

            sb.Append(entry);
        }

        return sb.ToString().TrimEnd();
    }

    private static string TruncateSummary(string summary, int maxChars)
    {
        // Account for the header line overhead (~30 chars)
        var available = Math.Max(50, maxChars - 30);

        if (summary.Length <= available)
            return summary.Trim();

        return summary[..(available - 3)].Trim() + "...";
    }
}
