using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class HubConnectionService(
    IOptions<SpokeConfiguration> config,
    ILogger<HubConnectionService> logger) : IHubConnectionService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly List<Action<HubConnection>> _handlerRegistrations = [];
    private SpokeInfo? _spokeInfo;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public Guid? SpokeId => _spokeInfo?.SpokeId ?? (Guid.TryParse(config.Value.Spoke.Id, out var id) ? id : null);

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var cfg = config.Value;
        var spokeId = cfg.Spoke.Id;
        var hubUrl = cfg.Hub.Url.TrimEnd('/');
        var url = $"{hubUrl}?spokeId={spokeId}";

        logger.LogInformation("Connecting to hub at {Url} as spoke {SpokeId}", cfg.Hub.Url, spokeId);

        _connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(cfg.Hub.Token);
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .Build();

        // Register all queued handlers before starting
        foreach (var register in _handlerRegistrations)
            register(_connection);

        // Register the SpokeRegistered callback once (not per-reconnect)
        _connection.On<SpokeRegisteredResponse>("SpokeRegistered", response =>
        {
            _spokeInfo = response.Info;
            logger.LogInformation("Registered with hub as {SpokeName} (id: {SpokeId})",
                response.Info.Name, response.Info.SpokeId);
            return Task.CompletedTask;
        });

        _connection.Reconnecting += error =>
        {
            logger.LogWarning(error, "Reconnecting to hub...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            logger.LogInformation("Reconnected to hub (connection {ConnectionId})", connectionId);
            _ = Task.Run(async () =>
            {
                try
                {
                    await RegisterSpokeAsync(cfg, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to re-register spoke after reconnection");
                }
            });
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            logger.LogWarning(error, "Hub connection closed");
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        logger.LogInformation("Connected to hub at {Url}", cfg.Hub.Url);

        await RegisterSpokeAsync(cfg, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(cancellationToken);
            logger.LogInformation("Disconnected from hub");
        }
    }

    public async Task SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            logger.LogWarning("Cannot send {Method}: not connected to hub", method);
            return;
        }

        await _connection.InvokeAsync(method, payload, cancellationToken);
    }

    public void OnReceived<T>(string method, Func<T, Task> handler)
    {
        _handlerRegistrations.Add(conn => conn.On(method, handler));
    }

    public void OnReceived<T1, T2>(string method, Func<T1, T2, Task> handler)
    {
        _handlerRegistrations.Add(conn => conn.On(method, handler));
    }

    private async Task RegisterSpokeAsync(SpokeConfiguration cfg, CancellationToken cancellationToken)
    {
        var capabilities = new List<string>();
        if (cfg.Capabilities.Jira) capabilities.Add("jira");
        if (cfg.Capabilities.Git) capabilities.Add("git");
        if (cfg.Capabilities.Docker) capabilities.Add("docker");
        if (cfg.Capabilities.PrMonitoring) capabilities.Add("pr_monitoring");

        var repos = cfg.GitProvider.Repositories
            .Select(r => new RepositoryDto(r.Name, r.RemoteUrl))
            .ToArray();

        var jiraConfig = cfg.Capabilities.Jira
            ? new JiraConfigDto(cfg.Jira.InstanceUrl, cfg.Jira.ProjectKeys)
            : null;

        var profile = new SpokeProfileDto(
            DisplayName: cfg.Spoke.Name,
            MachineDescription: $"{cfg.Spoke.Os}/{cfg.Spoke.Architecture}",
            Repos: repos,
            JiraConfig: jiraConfig,
            Integrations: capabilities.ToArray(),
            Description: $"Nexus Spoke - {cfg.Spoke.Name}"
        );

        var registration = new SpokeRegistration(
            Name: cfg.Spoke.Name,
            Capabilities: capabilities.ToArray(),
            Os: cfg.Spoke.Os,
            Architecture: cfg.Spoke.Architecture,
            Config: new SpokeConfigDto(
                ApprovalMode: cfg.Approval.Mode,
                MaxConcurrentJobs: cfg.Approval.MaxConcurrentJobs,
                HeartbeatIntervalSeconds: cfg.Approval.HeartbeatIntervalSeconds
            ),
            Profile: profile,
            Metadata: null
        );

        if (_connection is null) return;

        await _connection.InvokeAsync("RegisterSpoke", registration, cancellationToken);
        logger.LogDebug("Registration message sent to hub");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private const int InitialDelayMs = 1000;
        private const int MaxDelayMs = 300_000; // 5 minutes
        private const double Multiplier = 2.0;

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var delay = InitialDelayMs * Math.Pow(Multiplier, retryContext.PreviousRetryCount);
            delay = Math.Min(delay, MaxDelayMs);
            return TimeSpan.FromMilliseconds((int)delay);
        }
    }
}

// Response wrapper for SpokeRegistered callback
internal record SpokeRegisteredResponse(SpokeInfo Info, ReconnectionPolicy ReconnectionPolicy);
