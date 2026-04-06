using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Configuration;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddSpokeConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpokeConfiguration>(configuration);
        services.AddSingleton<IValidateOptions<SpokeConfiguration>, ConfigurationValidator>();
        services.AddOptionsWithValidateOnStart<SpokeConfiguration>();
        return services;
    }

    /// <summary>
    /// Resolves the default base path for the Nexus workspace based on OS.
    /// Linux/macOS: ~/.nexus, Windows: %LOCALAPPDATA%\Nexus
    /// </summary>
    internal static string GetDefaultBasePath()
    {
        return OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nexus");
    }

    /// <summary>
    /// Adds YAML configuration sources and environment variable overrides.
    /// </summary>
    public static IConfigurationBuilder AddSpokeConfigSources(this IConfigurationBuilder config)
    {
        var defaultBase = GetDefaultBasePath();

        config.AddYamlFile("config.yaml", optional: true, reloadOnChange: false);
        config.AddYamlFile("config.local.yaml", optional: true, reloadOnChange: false);

        // AddYamlFile with optional:true still throws DirectoryNotFoundException
        // if the parent directory doesn't exist, so guard against that.
        var defaultConfigPath = Path.Combine(defaultBase, "config.yaml");
        if (Directory.Exists(defaultBase))
        {
            config.AddYamlFile(defaultConfigPath, optional: true, reloadOnChange: false);
        }

        config.AddEnvironmentVariables("NEXUS_");

        return config;
    }
}
