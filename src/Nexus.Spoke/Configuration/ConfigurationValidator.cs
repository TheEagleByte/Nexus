using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Configuration;

public class ConfigurationValidator : IValidateOptions<SpokeConfiguration>
{
    public ValidateOptionsResult Validate(string? name, SpokeConfiguration options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Spoke.Id))
            failures.Add("Spoke:Id is required");
        if (string.IsNullOrWhiteSpace(options.Spoke.Name))
            failures.Add("Spoke:Name is required");
        if (string.IsNullOrWhiteSpace(options.Hub.Url))
            failures.Add("Hub:Url is required");
        else if (!Uri.TryCreate(options.Hub.Url, UriKind.Absolute, out _))
            failures.Add("Hub:Url must be a valid absolute URI");
        if (string.IsNullOrWhiteSpace(options.Hub.Token))
            failures.Add("Hub:Token is required");

        if (options.Approval.HeartbeatIntervalSeconds < 5)
            failures.Add("Approval:HeartbeatIntervalSeconds must be at least 5");
        if (options.Approval.MaxConcurrentJobs < 1)
            failures.Add("Approval:MaxConcurrentJobs must be at least 1");

        if (options.Capabilities.Docker)
        {
            if (options.Docker.TimeoutSeconds < 60)
                failures.Add("Docker:TimeoutSeconds must be at least 60 when Docker is enabled");
            if (options.Docker.ResourceLimits.MemoryBytes < 512_000_000)
                failures.Add("Docker:ResourceLimits:MemoryBytes must be at least 512MB");
            if (options.Docker.ResourceLimits.CpuCount < 1)
                failures.Add("Docker:ResourceLimits:CpuCount must be at least 1");

            // Validate credentials when network is enabled
            if (!string.Equals(options.Docker.NetworkMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                var creds = options.Docker.Credentials;
                var git = creds.Git;

                if (!string.Equals(git.AuthMethod, "ssh", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(git.AuthMethod, "token", StringComparison.OrdinalIgnoreCase))
                    failures.Add("Docker:Credentials:Git:AuthMethod must be 'ssh' or 'token'");

                if (string.IsNullOrWhiteSpace(git.UserName))
                    failures.Add("Docker:Credentials:Git:UserName is required when network is enabled");
                if (string.IsNullOrWhiteSpace(git.UserEmail))
                    failures.Add("Docker:Credentials:Git:UserEmail is required when network is enabled");

                if (string.Equals(git.AuthMethod, "ssh", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(git.SshKeyPath))
                    failures.Add("Docker:Credentials:Git:SshKeyPath is required when AuthMethod is 'ssh'");

                if (string.Equals(git.AuthMethod, "token", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(git.Token))
                    failures.Add("Docker:Credentials:Git:Token is required when AuthMethod is 'token'");
            }
        }

        if (options.GitProvider is { } gp)
        {
            if (!string.Equals(gp.Type, "github", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(gp.Type, "gitlab", StringComparison.OrdinalIgnoreCase))
                failures.Add("GitProvider:Type must be 'github' or 'gitlab'");

            if (string.IsNullOrWhiteSpace(gp.CredentialsRef))
            {
                failures.Add("GitProvider:CredentialsRef is required");
            }
            else if (string.Equals(gp.CredentialsRef, "docker", StringComparison.OrdinalIgnoreCase))
            {
                var git = options.Docker.Credentials.Git;
                if (string.IsNullOrWhiteSpace(git.Token) && string.IsNullOrWhiteSpace(git.SshKeyPath))
                    failures.Add("GitProvider:CredentialsRef is 'docker' but Docker:Credentials:Git has no token or SSH key configured");
            }
            else
            {
                failures.Add($"GitProvider:CredentialsRef '{gp.CredentialsRef}' is not a recognized credentials source (use 'docker')");
            }

            var repos = gp.Repositories ?? [];
            for (var i = 0; i < repos.Length; i++)
            {
                var repo = repos[i];
                if (string.IsNullOrWhiteSpace(repo.Name))
                    failures.Add($"GitProvider:Repositories[{i}]:Name is required");
                if (string.IsNullOrWhiteSpace(repo.RemoteUrl))
                    failures.Add($"GitProvider:Repositories[{i}]:RemoteUrl is required");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
