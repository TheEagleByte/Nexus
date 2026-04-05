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

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
