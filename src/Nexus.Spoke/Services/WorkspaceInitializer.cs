using Microsoft.Extensions.Options;
using Nexus.Spoke.Configuration;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class WorkspaceInitializer(
    IOptions<SpokeConfiguration> config,
    ILogger<WorkspaceInitializer> logger) : IHostedService
{
    private static readonly string[] Subdirectories = ["skills", "projects", "logs", "templates"];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var basePath = ResolveBasePath(config.Value);
        logger.LogInformation("Initializing workspace at {BasePath}", basePath);

        EnsureDirectoryExists(basePath);
        foreach (var sub in Subdirectories)
        {
            EnsureDirectoryExists(Path.Combine(basePath, sub));
        }

        logger.LogInformation("Workspace initialization complete");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static string ResolveBasePath(SpokeConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config.Workspace.BaseDirectory))
            return Environment.ExpandEnvironmentVariables(config.Workspace.BaseDirectory);

        return Nexus.Spoke.Configuration.ConfigurationExtensions.GetDefaultBasePath();
    }

    private void EnsureDirectoryExists(string path)
    {
        if (Directory.Exists(path))
        {
            logger.LogDebug("Directory already exists: {Path}", path);
            return;
        }

        Directory.CreateDirectory(path);
        logger.LogInformation("Created directory: {Path}", path);
    }
}
