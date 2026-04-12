using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Handlers;

public class JobAssignHandler(
    IProjectManager projectManager,
    IJiraService jiraService,
    IDockerService dockerService,
    IWorkerOutputStreamer outputStreamer,
    IJobLifecycleService lifecycleService,
    IJobArtifactService jobArtifacts,
    ISkillMerger skillMerger,
    IPromptAssembler promptAssembler,
    ActiveJobTracker activeJobTracker,
    IHostApplicationLifetime appLifetime,
    IOptions<SpokeConfiguration> config,
    ILogger<JobAssignHandler> logger) : ICommandHandler
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string CommandType => "job.assign";

    public async Task HandleAsync(CommandEnvelope command, CancellationToken cancellationToken)
    {
        var assignment = DeserializePayload(command.Payload);
        if (assignment is null)
        {
            logger.LogError("Failed to deserialize JobAssignment from command payload");
            return;
        }

        logger.LogInformation("Handling job assignment {JobId} for project {ProjectId}",
            assignment.JobId, assignment.ProjectId);

        // Resolve project key
        var projectKey = assignment.Parameters?.CustomFields?.TryGetValue("projectKey", out var keyObj) == true
            && keyObj is not null
            ? keyObj.ToString()!
            : assignment.ProjectId.ToString();

        // Create/find project
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
                    logger.LogWarning(ex, "Failed to fetch Jira ticket for {ProjectKey}", projectKey);
                }
            }
        }

        // Guard: Docker must be enabled
        if (!config.Value.Capabilities.Docker)
        {
            logger.LogError("Docker capability not enabled — cannot launch worker for job {JobId}", assignment.JobId);
            await lifecycleService.ReportStatusAsync(
                assignment.JobId, assignment.ProjectId, projectKey,
                JobStatus.Queued, JobStatus.Failed,
                "Docker capability not enabled on this spoke");
            return;
        }

        // Guard: concurrency limit
        if (activeJobTracker.Count >= config.Value.Approval.MaxConcurrentJobs)
        {
            logger.LogWarning("At max concurrent jobs ({Max}), cannot accept job {JobId}",
                config.Value.Approval.MaxConcurrentJobs, assignment.JobId);
            await lifecycleService.ReportStatusAsync(
                assignment.JobId, assignment.ProjectId, projectKey,
                JobStatus.Queued, JobStatus.Failed,
                $"Spoke at capacity ({config.Value.Approval.MaxConcurrentJobs} concurrent jobs)");
            return;
        }

        try
        {
            await LaunchWorkerAsync(assignment, projectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch worker for job {JobId}", assignment.JobId);
            await lifecycleService.ReportStatusAsync(
                assignment.JobId, assignment.ProjectId, projectKey,
                JobStatus.Queued, JobStatus.Failed,
                $"Failed to launch worker: {ex.Message}");
        }
    }

    private async Task LaunchWorkerAsync(JobAssignment assignment, string projectKey, CancellationToken cancellationToken)
    {
        // Ensure Docker image is available
        await dockerService.EnsureImageAsync(cancellationToken);

        // Initialize job directory
        var jobDir = await jobArtifacts.InitializeJobAsync(projectKey, assignment.JobId);
        var promptPath = Path.Combine(jobDir, "prompt.md");

        var outputPath = Path.Combine(jobDir, "output");
        Directory.CreateDirectory(outputPath);

        // Build repo init config for container-side cloning
        var repoInitConfig = BuildRepoInitConfig(assignment, projectKey);
        var repoConfigFilePath = Path.Combine(jobDir, "repo-config.json");
        var repoConfigJson = JsonSerializer.Serialize(repoInitConfig, SerializeOptions);
        await File.WriteAllTextAsync(repoConfigFilePath, repoConfigJson, cancellationToken);

        // Resolve workspace paths
        var basePath = projectManager.GetProjectPath(projectKey);

        var spokeSkillsPath = Path.Combine(
            WorkspaceInitializer.ResolveBasePath(config.Value), "skills");
        var projectSkillsPath = Path.Combine(basePath, ".nexus", "skills");

        // Merge spoke + project skills
        var mergedSkills = await skillMerger.MergeSkillsAsync(
            spokeSkillsPath,
            Directory.Exists(projectSkillsPath) ? projectSkillsPath : null,
            cancellationToken);

        string? mergedSkillsFilePath = null;
        if (mergedSkills is not null)
        {
            mergedSkillsFilePath = Path.Combine(jobDir, "merged-skills.md");
            await File.WriteAllTextAsync(mergedSkillsFilePath, mergedSkills, cancellationToken);
        }

        // Assemble prompt from template + context sources
        var metaPath = projectManager.GetMetaPath(projectKey);
        var contextMd = await ReadFileOrNullAsync(Path.Combine(metaPath, "context.md"), cancellationToken);
        var planMd = await ReadFileOrNullAsync(Path.Combine(metaPath, "plan.md"), cancellationToken);

        TicketMetadata? ticket = null;
        var ticketPath = Path.Combine(metaPath, "ticket.json");
        if (File.Exists(ticketPath))
        {
            try
            {
                var ticketJson = await File.ReadAllTextAsync(ticketPath, cancellationToken);
                ticket = JsonSerializer.Deserialize<TicketMetadata>(ticketJson, DeserializeOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize ticket metadata for {ProjectKey}", projectKey);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Failed to read ticket metadata file for {ProjectKey}", projectKey);
            }
        }

        var assemblyContext = new PromptAssemblyContext(
            assignment.JobId,
            assignment.Type,
            projectKey,
            assignment.Context,
            ticket,
            contextMd,
            planMd,
            spokeSkillsPath,
            projectSkillsPath);

        var assembledPrompt = await promptAssembler.AssembleAsync(assemblyContext, cancellationToken);
        await jobArtifacts.WritePromptAsync(projectKey, assignment.JobId, assembledPrompt);

        // Report status: Queued → Running
        await lifecycleService.ReportStatusAsync(
            assignment.JobId, assignment.ProjectId, projectKey,
            JobStatus.Queued, JobStatus.Running);

        // Launch the container
        var request = new WorkerLaunchRequest(
            assignment.JobId,
            projectKey,
            assignment.Type,
            promptPath,
            repoConfigFilePath,
            outputPath,
            spokeSkillsPath,
            projectSkillsPath,
            mergedSkillsFilePath);

        var containerId = await dockerService.LaunchWorkerAsync(request, cancellationToken);

        // Create a linked CTS so spoke shutdown cancels all jobs
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.ApplicationStopping);

        var activeJob = new ActiveJob(
            assignment.JobId,
            assignment.ProjectId,
            projectKey,
            containerId,
            jobCts,
            DateTimeOffset.UtcNow);

        if (!activeJobTracker.TryAdd(assignment.JobId, activeJob))
        {
            logger.LogWarning("Job {JobId} already tracked, killing duplicate container", assignment.JobId);
            await dockerService.KillContainerAsync(containerId, CancellationToken.None);
            await dockerService.RemoveContainerAsync(containerId, CancellationToken.None);
            jobCts.Dispose();
            return;
        }

        // Fire-and-forget the monitoring loop
        _ = Task.Run(() => MonitorJobAsync(assignment, projectKey, containerId, jobCts), CancellationToken.None);
    }

    private async Task MonitorJobAsync(
        JobAssignment assignment, string projectKey, string containerId, CancellationTokenSource jobCts)
    {
        var jobToken = jobCts.Token;

        try
        {
            // Stream output and wait for exit in parallel
            var streamTask = outputStreamer.StreamAsync(
                assignment.JobId, assignment.ProjectId, projectKey, containerId, jobToken);
            var waitTask = dockerService.WaitForExitAsync(containerId, jobToken);

            // Wait for the container to exit (streaming completes when container stops)
            await Task.WhenAll(streamTask, waitTask);

            var exitCode = await waitTask;

            if (exitCode == 0)
            {
                await lifecycleService.ReportStatusAsync(
                    assignment.JobId, assignment.ProjectId, projectKey,
                    JobStatus.Running, JobStatus.Completed,
                    "Job completed successfully");
            }
            else
            {
                await lifecycleService.ReportStatusAsync(
                    assignment.JobId, assignment.ProjectId, projectKey,
                    JobStatus.Running, JobStatus.Failed,
                    $"Worker exited with code {exitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is handled by JobCancelHandler or JobTimeoutMonitor
            logger.LogInformation("Job {JobId} monitoring cancelled", assignment.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error monitoring job {JobId}", assignment.JobId);

            try
            {
                await lifecycleService.ReportStatusAsync(
                    assignment.JobId, assignment.ProjectId, projectKey,
                    JobStatus.Running, JobStatus.Failed,
                    $"Monitoring error: {ex.Message}");
            }
            catch
            {
                // Best-effort status reporting
            }
        }
        finally
        {
            // Cleanup
            activeJobTracker.TryRemove(assignment.JobId, out _);

            try
            {
                await dockerService.RemoveContainerAsync(containerId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove container for job {JobId}", assignment.JobId);
            }

            jobCts.Dispose();
        }
    }

    private RepoInitConfig BuildRepoInitConfig(JobAssignment assignment, string projectKey)
    {
        var gitProviderConfig = config.Value.GitProvider;

        var repoConfig = new RepoInitConfig
        {
            BranchTemplate = gitProviderConfig.BranchTemplate,
            JobType = assignment.Type.ToString().ToLowerInvariant(),
            ProjectKey = projectKey,
            JobId = assignment.JobId.ToString()
        };

        // Build repo list from GitProvider config
        if (gitProviderConfig.Repositories.Length > 0)
        {
            foreach (var repo in gitProviderConfig.Repositories)
            {
                repoConfig.Repositories.Add(new RepoEntry
                {
                    Name = repo.Name,
                    CloneUrl = repo.RemoteUrl,
                    DefaultBranch = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? "main" : repo.DefaultBranch
                });
            }
        }

        // If job parameters specify repos, those override the global config
        if (assignment.Parameters?.CustomFields?.TryGetValue("repositories", out var reposObj) == true
            && reposObj is JsonElement reposElement
            && reposElement.ValueKind == JsonValueKind.Array)
        {
            repoConfig.Repositories.Clear();
            foreach (var repoElement in reposElement.EnumerateArray())
            {
                var entry = JsonSerializer.Deserialize<RepoEntry>(repoElement.GetRawText(), DeserializeOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.CloneUrl))
                    throw new InvalidOperationException("Invalid repository override entry: name and cloneUrl are required.");
                entry.DefaultBranch = string.IsNullOrWhiteSpace(entry.DefaultBranch) ? "main" : entry.DefaultBranch;
                repoConfig.Repositories.Add(entry);
            }
        }

        logger.LogInformation("Built repo init config with {Count} repositories for job {JobId}",
            repoConfig.Repositories.Count, assignment.JobId);

        return repoConfig;
    }

    private static async Task<string?> ReadFileOrNullAsync(string path, CancellationToken cancellationToken = default)
        => File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;

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
