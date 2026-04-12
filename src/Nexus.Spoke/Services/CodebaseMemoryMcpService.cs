using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class CodebaseMemoryMcpService(
    IOptions<SpokeConfiguration> config,
    ILogger<CodebaseMemoryMcpService> logger) : ICodebaseMemoryMcpService, IDisposable
{
    private readonly object _lock = new();
    private Process? _process;
    private volatile CodebaseMemoryMcpStatus _status = CodebaseMemoryMcpStatus.Stopped;
    private string? _lastError;

    public async Task StartAsync(CancellationToken ct)
    {
        var mcpConfig = config.Value.CodebaseMemoryMcp;
        if (!mcpConfig.Enabled)
        {
            _status = CodebaseMemoryMcpStatus.Disabled;
            logger.LogInformation("Codebase Memory MCP server is disabled");
            return;
        }

        _status = CodebaseMemoryMcpStatus.Starting;
        logger.LogInformation("Starting Codebase Memory MCP server on port {Port}", mcpConfig.Port);

        var reposPath = GetReposPath();
        var indexPath = GetIndexPath();

        Directory.CreateDirectory(indexPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = mcpConfig.NpxCommand,
            Arguments = $"{mcpConfig.PackageName} --port {mcpConfig.Port} --repos \"{reposPath}\" --index \"{indexPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            lock (_lock)
            {
                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _process.Exited += OnProcessExited;
                _process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        logger.LogDebug("[MCP stdout] {Data}", args.Data);
                };
                _process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        logger.LogDebug("[MCP stderr] {Data}", args.Data);
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            // Wait for startup with timeout
            var timeout = TimeSpan.FromSeconds(mcpConfig.StartupTimeoutSeconds);
            var startedAt = Stopwatch.StartNew();

            while (startedAt.Elapsed < timeout && !ct.IsCancellationRequested)
            {
                Process? proc;
                lock (_lock)
                {
                    proc = _process;
                }

                if (proc is null || proc.HasExited)
                {
                    _status = CodebaseMemoryMcpStatus.Failed;
                    _lastError = proc is null
                        ? "MCP server process was stopped during startup"
                        : $"MCP server process exited with code {proc.ExitCode} during startup";
                    logger.LogError("MCP server process exited during startup");
                    return;
                }

                // Check if the port is listening
                if (await IsPortListeningAsync(mcpConfig.Port, ct))
                {
                    _status = CodebaseMemoryMcpStatus.Running;
                    logger.LogInformation("Codebase Memory MCP server started successfully on port {Port}", mcpConfig.Port);
                    return;
                }

                await Task.Delay(500, ct);
            }

            if (ct.IsCancellationRequested) return;

            // Timeout — process started but never became ready.
            _status = CodebaseMemoryMcpStatus.Failed;
            _lastError = $"MCP server startup timed out after {mcpConfig.StartupTimeoutSeconds}s";
            logger.LogWarning("MCP server startup timed out after {Timeout}s — port never became ready", mcpConfig.StartupTimeoutSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _status = CodebaseMemoryMcpStatus.Failed;
            _lastError = ex.Message;
            logger.LogError(ex, "Failed to start Codebase Memory MCP server");

            // Clean up the process object if Start() failed
            lock (_lock)
            {
                _process?.Dispose();
                _process = null;
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            if (_process is null || _process.HasExited)
            {
                _status = CodebaseMemoryMcpStatus.Stopped;
                return;
            }
        }

        logger.LogInformation("Stopping Codebase Memory MCP server");

        try
        {
            lock (_lock)
            {
                if (_process is not null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
        finally
        {
            lock (_lock)
            {
                _process?.Dispose();
                _process = null;
            }
            _status = CodebaseMemoryMcpStatus.Stopped;
            logger.LogInformation("Codebase Memory MCP server stopped");
        }

        await Task.CompletedTask;
    }

    public bool IsHealthy()
    {
        if (_status != CodebaseMemoryMcpStatus.Running)
            return false;

        lock (_lock)
        {
            return _process is not null && !_process.HasExited;
        }
    }

    public async Task ReindexAsync(CancellationToken ct)
    {
        if (!config.Value.CodebaseMemoryMcp.Enabled || _status != CodebaseMemoryMcpStatus.Running)
        {
            logger.LogDebug("Skipping reindex: MCP server not running");
            return;
        }

        logger.LogInformation("Triggering MCP server reindex via restart");

        // Restart the process to trigger re-indexing
        await StopAsync(ct);
        await StartAsync(ct);
    }

    public string? GetEndpoint()
    {
        if (_status != CodebaseMemoryMcpStatus.Running)
            return null;

        // Containers use host.docker.internal to reach host services;
        // localhost would resolve to the container's own loopback.
        return $"http://host.docker.internal:{config.Value.CodebaseMemoryMcp.Port}";
    }

    public CodebaseMemoryMcpStatus GetStatus() => _status;

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_status == CodebaseMemoryMcpStatus.Running)
        {
            _status = CodebaseMemoryMcpStatus.Failed;
            _lastError = "MCP server process exited unexpectedly";
            logger.LogWarning("Codebase Memory MCP server process exited unexpectedly");
        }
    }

    private string GetReposPath()
    {
        var basePath = WorkspaceInitializer.ResolveBasePath(config.Value);
        return Path.GetFullPath(Path.Combine(basePath, "repos"));
    }

    private string GetIndexPath()
    {
        var mcpConfig = config.Value.CodebaseMemoryMcp;
        if (!string.IsNullOrWhiteSpace(mcpConfig.IndexPath))
            return Path.GetFullPath(mcpConfig.IndexPath);

        var basePath = WorkspaceInitializer.ResolveBasePath(config.Value);
        return Path.GetFullPath(Path.Combine(basePath, "mcp-index"));
    }

    private static async Task<bool> IsPortListeningAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(1));
            await client.ConnectAsync("127.0.0.1", port, connectCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process was never started or already exited
                }
                _process.Dispose();
                _process = null;
            }
        }
    }
}
