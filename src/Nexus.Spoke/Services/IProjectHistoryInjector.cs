namespace Nexus.Spoke.Services;

public interface IProjectHistoryInjector
{
    Task<string> GetHistorySummaryAsync(
        string projectKey,
        Guid currentJobId,
        int maxEntries = 5,
        int maxTotalChars = 3000,
        CancellationToken cancellationToken = default);
}
