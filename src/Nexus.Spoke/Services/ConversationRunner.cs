using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class ConversationRunner(
    IOptions<SpokeConfiguration> config,
    ILogger<ConversationRunner> logger) : IConversationRunner
{
    public async Task<string> InvokeAsync(string? ccSessionId, string message, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? config.Value.Workspace.BaseDirectory
        };

        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("text");

        if (!string.IsNullOrEmpty(ccSessionId))
        {
            psi.ArgumentList.Add("--resume");
            psi.ArgumentList.Add(ccSessionId);
        }

        psi.ArgumentList.Add(message);

        logger.LogInformation("Invoking CC CLI{Resume} in {WorkingDir}",
            string.IsNullOrEmpty(ccSessionId) ? "" : $" --resume {ccSessionId}",
            psi.WorkingDirectory);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start CC CLI process");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            logger.LogError("CC CLI failed (exit {ExitCode}): {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"CC CLI exited with code {process.ExitCode}: {stderr}");
        }

        logger.LogInformation("CC CLI responded ({Length} chars)", stdout.Length);
        return stdout.Trim();
    }
}
