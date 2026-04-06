namespace Nexus.Spoke.Services;

public static class MemoryInitializer
{
    private static readonly Dictionary<string, string> MemoryTemplates = new()
    {
        ["global.md"] = """
            # Global Knowledge

            Cross-project knowledge accumulated by the spoke agent.
            Add entries as you discover patterns, gotchas, or conventions that apply across projects.

            ## Format

            - **Topic**: Brief description of the knowledge
            - **Context**: When/where this applies
            - **Details**: The actual knowledge

            """,

        ["codebase-notes.md"] = """
            # Codebase Notes

            Repository-specific patterns, architecture decisions, and conventions.
            Updated as the agent learns about each codebase it works with.

            ## Format

            ### [Repository Name]
            - **Stack**: Technologies used
            - **Patterns**: Key architectural patterns
            - **Conventions**: Naming, structure, testing conventions

            """,

        ["decision-log.md"] = """
            # Decision Log

            Key decisions made during project work, with rationale.

            ## Format

            ### YYYY-MM-DD — [Decision Title]
            - **Context**: What prompted this decision
            - **Decision**: What was decided
            - **Rationale**: Why this choice was made
            - **Consequences**: Expected impact

            """
    };

    public static async Task InitializeAsync(string memoriesDirectory, ILogger logger, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(memoriesDirectory);

        foreach (var (fileName, template) in MemoryTemplates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(memoriesDirectory, fileName);
            if (File.Exists(filePath))
            {
                logger.LogDebug("Memory file already exists: {Path}", filePath);
                continue;
            }

            // Dedent the raw string literal template
            var content = Dedent(template);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            logger.LogInformation("Created memory file: {Path}", filePath);
        }
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
