using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class ConversationRunner(
    IOptions<SpokeConfiguration> config,
    ILogger<ConversationRunner> logger) : IConversationRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public async Task<string> InvokeAsync(string? ccSessionId, string message, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var baseDir = workingDirectory ?? config.Value.Workspace.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus");

        Directory.CreateDirectory(baseDir);

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = baseDir
        };

        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("text");

        if (!string.IsNullOrEmpty(ccSessionId))
        {
            psi.ArgumentList.Add("--resume");
            psi.ArgumentList.Add(ccSessionId);
        }

        // Pass prompt via stdin to avoid exposing content in process listings
        logger.LogInformation("Invoking CC CLI{Resume} in {WorkingDir}",
            string.IsNullOrEmpty(ccSessionId) ? "" : $" --resume {ccSessionId}",
            psi.WorkingDirectory);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start CC CLI process");

        try
        {
            // Write prompt to stdin and close to signal EOF
            await process.StandardInput.WriteAsync(message);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(timeoutCts.Token));

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogError("CC CLI failed (exit {ExitCode}): {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException($"CC CLI exited with code {process.ExitCode}");
            }

            logger.LogInformation("CC CLI responded ({Length} chars)", stdout.Length);
            return stdout.Trim();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException($"CC CLI did not respond within {DefaultTimeout.TotalMinutes} minutes");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
    }
}
