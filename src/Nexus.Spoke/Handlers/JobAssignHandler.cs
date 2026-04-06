using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Handlers;

public class JobAssignHandler(
    IProjectManager projectManager,
    IJiraService jiraService,
    IOptions<SpokeConfiguration> config,
    ILogger<JobAssignHandler> logger) : ICommandHandler
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string CommandType => "job.assign";

    public async Task HandleAsync(CommandEnvelope command, CancellationToken cancellationToken)
    {
        var assignment = DeserializePayload(command.Payload);
        if (assignment is null)
        {
            // Malformed commands are dropped — the CommandQueueWorker already logs errors
            // for handler exceptions, so we log and return to avoid blocking the queue.
            logger.LogError("Failed to deserialize JobAssignment from command payload");
            return;
        }

        logger.LogInformation("Handling job assignment {JobId} for project {ProjectId}",
            assignment.JobId, assignment.ProjectId);

        // Use the project key from parameters, or fall back to ProjectId as the key
        var projectKey = assignment.Parameters?.CustomFields?.TryGetValue("projectKey", out var keyObj) == true
            && keyObj is not null
            ? keyObj.ToString()!
            : assignment.ProjectId.ToString();

        var existing = await projectManager.GetProjectAsync(projectKey);
        if (existing is null)
        {
            logger.LogInformation("Creating new project {ProjectKey} from hub directive", projectKey);

            await projectManager.CreateProjectAsync(projectKey, projectKey);

            if (config.Value.Capabilities.Jira)
            {
                try
                {
                    var ticket = await jiraService.FetchTicketAsync(projectKey, cancellationToken);
                    if (ticket is not null)
                    {
                        await projectManager.SaveTicketMetadataAsync(projectKey, ticket);
                        logger.LogInformation("Cached Jira ticket {Key} for project", projectKey);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch Jira ticket for {ProjectKey}, continuing without ticket data", projectKey);
                }
            }
        }

        logger.LogInformation("Job {JobId} assigned — project {ProjectKey} ready (job execution not yet implemented)",
            assignment.JobId, projectKey);
    }

    private static JobAssignment? DeserializePayload(object payload)
    {
        if (payload is JobAssignment assignment)
            return assignment;

        if (payload is JsonElement element)
            return JsonSerializer.Deserialize<JobAssignment>(element.GetRawText(), DeserializeOptions);

        var json = JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<JobAssignment>(json, DeserializeOptions);
    }
}
