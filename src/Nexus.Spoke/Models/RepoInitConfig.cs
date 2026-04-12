namespace Nexus.Spoke.Models;

/// <summary>
/// Configuration passed to worker containers for repository initialization.
/// Serialized to JSON and mounted at /workspace/repo-config.json.
/// </summary>
public class RepoInitConfig
{
    public List<RepoEntry> Repositories { get; set; } = new();
    public string BranchTemplate { get; set; } = "nexus/{type}/{key}";
    public string JobType { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public string JobId { get; set; } = "";
}

/// <summary>
/// A single repository to be cloned inside the worker container.
/// </summary>
public class RepoEntry
{
    public string Name { get; set; } = "";
    public string CloneUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
}
