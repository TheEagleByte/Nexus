namespace Nexus.Spoke.Services;

public class GitOperationException(string command, int exitCode, string standardError)
    : Exception($"Git command failed (exit {exitCode}): git {command}\n{standardError}")
{
    public string Command { get; } = command;
    public int ExitCode { get; } = exitCode;
    public string StandardError { get; } = standardError;
}
