using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IJobLifecycleService
{
    Task ReportStatusAsync(
        Guid jobId,
        Guid projectId,
        string projectKey,
        JobStatus previousStatus,
        JobStatus newStatus,
        string? summary = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
}
