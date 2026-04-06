namespace Nexus.Spoke.Services;

public static class TemplateInitializer
{
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["worker-prompt-base.md"] = """
            # Worker Prompt — {TICKET_KEY}: {TICKET_SUMMARY}

            **Job ID**: {JOB_ID}
            **Job Type**: {JOB_TYPE}
            **Project**: {PROJECT_KEY}
            **Issue Type**: {ISSUE_TYPE}
            **Generated**: {TIMESTAMP}

            ---

            ## Task Description

            {TICKET_DESCRIPTION}

            ## Acceptance Criteria

            {ACCEPTANCE_CRITERIA}

            ## Hub Context

            {HUB_CONTEXT}

            ---

            ## Project Context

            {PROJECT_CONTEXT}

            ## Implementation Plan

            {IMPLEMENTATION_PLAN}

            ## Relevant Skills

            {SKILL_GUIDANCE}

            ## Prior Work on This Project

            {PROJECT_HISTORY}

            ---

            ## Worker Instructions

            ### Coding Conventions
            - Follow existing patterns in the codebase — naming, formatting, structure.
            - Write idiomatic code for the language/framework in use.
            - Keep changes minimal and focused on the task. Do not refactor unrelated code.

            ### Output Expectations
            - Write a `summary.md` in the output directory describing what you did and why.
            - Make atomic commits with messages in the format: `{TICKET_KEY}: description of change`
            - If creating a PR, use the ticket key in the title.

            ### Error Handling
            - If you encounter a blocker that prevents completing the task, write the blocker details to `summary.md` and exit.
            - Do not silently skip acceptance criteria. If one cannot be met, document why.

            ### Testing
            - Write or update tests for new/changed functionality.
            - Ensure all existing tests pass before finishing.

            ### Scope
            - Stay within the bounds of the ticket. Do not add features, refactor beyond scope, or "improve" adjacent code.
            - If you discover issues outside scope, note them in `summary.md` but do not fix them.
            """,

        ["plan-template.md"] = """
            # Implementation Plan: {TICKET_KEY}

            ## Objective

            {TICKET_SUMMARY}

            ## Requirements

            {TICKET_DESCRIPTION}

            ## Acceptance Criteria

            {ACCEPTANCE_CRITERIA}

            ## Approach

            <!-- Agent fills this section during planning phase -->

            ## Tasks

            <!-- Agent fills this section during planning phase -->
            <!-- Format: numbered list of discrete, verifiable steps -->

            ## Risks & Open Questions

            <!-- Agent fills this section during planning phase -->
            """
    };

    public static async Task InitializeAsync(string templatesDirectory, ILogger logger, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(templatesDirectory);

        foreach (var (fileName, template) in Templates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(templatesDirectory, fileName);
            if (File.Exists(filePath))
            {
                logger.LogDebug("Template file already exists: {Path}", filePath);
                continue;
            }

            var content = Dedent(template);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogInformation("Created template file: {Path}", filePath);
        }
    }

    internal static IReadOnlyCollection<string> GetTemplateNames() => Templates.Keys;

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
