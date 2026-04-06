using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public static class SkillsInitializer
{
    private static readonly string[] Subdirectories = ["conventions", "templates"];

    private static readonly Dictionary<string, string> SkillTemplates = new()
    {
        ["conventions/workspace-management.md"] = """
            # Workspace Management

            ## Directory Structure

            The spoke workspace is organized as:

            ```
            {BaseDirectory}/
            ├── skills/           — Skill files (this directory)
            │   ├── CLAUDE.md     — Spoke identity and capabilities
            │   ├── conventions/  — Behavioral conventions
            │   └── templates/    — Output templates
            ├── projects/         — Project workspaces
            │   └── {PROJECT-KEY}/
            │       ├── repo/     — Cloned repository
            │       ├── jobs/     — Job artifacts and output
            │       ├── .nexus/   — Project-level overrides
            │       │   └── skills/
            │       └── metadata.json
            ├── memories/         — Cross-project knowledge
            ├── logs/             — Operational logs
            └── templates/        — Workspace templates
            ```

            ## Project Lifecycle

            Projects transition through these states:

            - **Planning** — Initial state after creation. Metadata being gathered.
            - **Active** — Work is underway. Jobs can be assigned.
            - **Paused** — Temporarily halted. No new jobs accepted.
            - **Completed** — All work finished. Read-only archive.
            - **Failed** — Terminal failure. Requires manual intervention.

            Valid transitions: Planning → Active, Active → Paused/Completed/Failed,
            Paused → Active/Failed.

            ## Project Operations

            - **Create**: `CreateProjectAsync(key, name)` — creates directory structure, sets Planning status
            - **List**: `ListProjectsAsync()` — returns all projects with current status
            - **Get**: `GetProjectAsync(key)` — returns single project info
            - **Update Status**: `UpdateProjectStatusAsync(key, newStatus)` — validates transition rules

            When a job arrives for an unknown project, the spoke auto-creates it in Planning status,
            then fetches Jira metadata if the Jira capability is enabled.

            """,

        ["conventions/job-orchestration.md"] = """
            # Job Orchestration

            ## Job Lifecycle

            Jobs flow through these states:

            - **Queued** — Received from hub, waiting for resources
            - **Running** — Worker container launched, Claude Code executing
            - **Completed** — Worker exited successfully (exit code 0)
            - **Failed** — Worker exited with error or timed out
            - **Cancelled** — Cancelled by hub or operator

            ## Worker Containers

            Each job runs in an isolated Docker container with:

            - **Network**: Disabled (`none`) — no internet access, no exfiltration risk
            - **Capabilities**: All dropped (`CapDrop=ALL`)
            - **Filesystem**: Read-only root (writable `/tmp` with noexec)
            - **User**: Unprivileged (UID 1000)
            - **Resources**: Configurable CPU and memory limits (default: 2 CPU, 8 GB RAM)
            - **Timeout**: Configurable per-spoke (default: 4 hours)

            ## Container Mounts

            | Mount Point | Mode | Contents |
            |---|---|---|
            | `/workspace/repo` | rw | Repository source code |
            | `/workspace/prompt.md` | ro | Job prompt/instructions |
            | `/workspace/output` | rw | Job output artifacts |
            | `/workspace/skills/spoke` | ro | Spoke-level skills |
            | `/workspace/skills/project` | ro | Project-level skills |
            | `/workspace/skills/CLAUDE.md` | ro | Merged skills CLAUDE.md |

            ## Concurrency

            The spoke enforces a configurable maximum concurrent job limit (default: 1).
            Jobs arriving when at capacity are rejected back to the hub with a capacity error.

            ## Output Streaming

            Worker stdout/stderr is streamed in real-time back to the hub via SignalR.
            Claude Code runs with `--output-format stream-json` for structured output parsing.

            """,

        ["conventions/conversation-management.md"] = """
            # Conversation Management

            ## Claude Code Sessions

            The spoke can maintain a persistent Claude Code session ID (`CcSessionId` in config)
            for resuming conversations across spoke restarts.

            ## Memory Files

            Cross-project knowledge is stored in `{BaseDirectory}/memories/`:

            - `global.md` — Patterns, gotchas, and conventions that apply across projects
            - `codebase-notes.md` — Repository-specific architecture and conventions
            - `decision-log.md` — Key decisions with rationale and consequences

            Update these files when discovering information that will be valuable across
            future jobs and projects.

            ## Hub Communication

            The spoke communicates with the hub via SignalR WebSocket:

            - **Outbound**: Status updates, heartbeats, output streaming
            - **Inbound**: Job assignments, cancellations, configuration updates

            Heartbeats include resource utilization (CPU, memory, disk) so the hub can
            make informed scheduling decisions.

            ## Job Artifacts

            Each job produces artifacts stored at `projects/{KEY}/jobs/{JOB-ID}/`:

            - `prompt.md` — The original job prompt
            - `output/` — Any files produced by the worker
            - `merged-skills.md` — The merged skills injected into the worker

            """,

        ["templates/implementation-plan.md"] = """
            # Implementation Plan

            ## Overview

            _Brief description of what this plan accomplishes and why._

            ## Requirements

            - [ ] _Requirement 1_
            - [ ] _Requirement 2_

            ## File Changes

            | File | Action | Description |
            |---|---|---|
            | `path/to/file` | Create/Modify/Delete | _What changes_ |

            ## Implementation Steps

            1. _Step 1_
            2. _Step 2_

            ## Test Plan

            - [ ] _Test scenario 1_
            - [ ] _Test scenario 2_

            ## Rollback Plan

            _How to revert if something goes wrong._

            """,

        ["templates/job-summary.md"] = """
            # Job Summary

            ## What Changed

            - _Change 1_
            - _Change 2_

            ## Files Modified

            | File | Changes |
            |---|---|
            | `path/to/file` | _Description_ |

            ## Tests

            - [ ] _Test result 1_
            - [ ] _Test result 2_

            ## Known Issues

            _Any issues discovered during implementation._

            ## Follow-up Items

            - [ ] _Follow-up 1_
            - [ ] _Follow-up 2_

            """
    };

    public static async Task InitializeAsync(
        string skillsDirectory, SpokeConfiguration config,
        ILogger logger, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(skillsDirectory);

        foreach (var sub in Subdirectories)
        {
            var subDir = Path.Combine(skillsDirectory, sub);
            if (!Directory.Exists(subDir))
            {
                Directory.CreateDirectory(subDir);
                logger.LogInformation("Created skills subdirectory: {Path}", subDir);
            }
        }

        // Generate dynamic CLAUDE.md from spoke configuration
        var claudeMdPath = Path.Combine(skillsDirectory, "CLAUDE.md");
        if (!File.Exists(claudeMdPath))
        {
            var content = GenerateClaudeMd(config);
            await File.WriteAllTextAsync(claudeMdPath, content, cancellationToken);
            logger.LogInformation("Created skills CLAUDE.md: {Path}", claudeMdPath);
        }

        // Write static skill templates
        foreach (var (relativePath, template) in SkillTemplates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(skillsDirectory, relativePath);
            if (File.Exists(filePath))
            {
                logger.LogDebug("Skill file already exists: {Path}", filePath);
                continue;
            }

            var parentDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(parentDir);

            var content = Dedent(template);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogInformation("Created skill file: {Path}", filePath);
        }
    }

    private static string GenerateClaudeMd(SpokeConfiguration config)
    {
        var sb = new System.Text.StringBuilder();

        var spokeName = !string.IsNullOrEmpty(config.Spoke.Name) ? config.Spoke.Name : "Unnamed Spoke";
        sb.AppendLine($"# Nexus Spoke Agent — {spokeName}");
        sb.AppendLine();

        // Machine info
        var os = !string.IsNullOrEmpty(config.Spoke.Os) ? config.Spoke.Os : "Unknown OS";
        var arch = !string.IsNullOrEmpty(config.Spoke.Architecture) ? config.Spoke.Architecture : "Unknown Architecture";
        sb.AppendLine($"Running on **{os}** ({arch}).");
        sb.AppendLine();

        // Capabilities
        sb.AppendLine("## Capabilities");
        sb.AppendLine();
        var caps = config.Capabilities;
        if (caps.Docker) sb.AppendLine("- Docker: Worker container orchestration");
        if (caps.Git) sb.AppendLine("- Git: Repository cloning and management");
        if (caps.Jira) sb.AppendLine("- Jira: Ticket metadata fetching");
        if (caps.PrMonitoring) sb.AppendLine("- PR Monitoring: Pull request status tracking");
        if (!caps.Docker && !caps.Git && !caps.Jira && !caps.PrMonitoring)
            sb.AppendLine("_No capabilities enabled._");
        sb.AppendLine();

        // Role description
        sb.AppendLine("## Role");
        sb.AppendLine();
        sb.AppendLine("You are an AI coding agent running on a Nexus spoke. You receive jobs from the");
        sb.AppendLine("central hub and execute them in isolated Docker containers. Your work products");
        sb.AppendLine("are streamed back to the hub in real-time.");
        sb.AppendLine();

        // Instructions
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("- Follow the conventions in `conventions/` for workspace, job, and conversation management");
        sb.AppendLine("- Use templates in `templates/` for structured output (plans, summaries)");
        sb.AppendLine("- Update memory files in `memories/` when discovering cross-project knowledge");
        sb.AppendLine("- Report job status accurately — the hub relies on status transitions for orchestration");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Dedent(string text)
    {
        var lines = text.Split('\n');
        var minIndent = lines
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        return string.Join('\n', lines.Select(l => l.Length >= minIndent ? l[minIndent..] : l)).Trim() + "\n";
    }
}
