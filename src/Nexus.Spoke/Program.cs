using Nexus.Spoke.Configuration;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Nexus Spoke starting");

    var builder = Host.CreateApplicationBuilder(args);

    // NEX-124: Configuration — appsettings.json + config.yaml + env vars
    builder.Configuration.AddSpokeConfigSources();
    builder.Services.AddSpokeConfiguration(builder.Configuration);

    // Logging
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    // NEX-142: Workspace initialization (runs first)
    builder.Services.AddHostedService<WorkspaceInitializer>();

    // NEX-130: Hub connection
    builder.Services.AddSingleton<IHubConnectionService, HubConnectionService>();

    // NEX-2: Project management services
    builder.Services.AddSingleton<IProjectManager, ProjectManager>();
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IJiraService, JiraService>();
    builder.Services.AddSingleton<IJobArtifactService, JobArtifactService>();

    // NEX-125: Skills merge service
    builder.Services.AddSingleton<ISkillMerger, SkillMerger>();

    // NEX-43: Prompt assembly pipeline
    builder.Services.AddSingleton<ISkillSelector, SkillSelector>();
    builder.Services.AddSingleton<IProjectHistoryInjector, ProjectHistoryInjector>();
    builder.Services.AddSingleton<IPromptAssembler, PromptAssembler>();

    // NEX-4: Docker integration & worker launching
    builder.Services.AddSingleton<IDockerService, DockerService>();
    builder.Services.AddSingleton<IWorkerOutputStreamer, WorkerOutputStreamer>();
    builder.Services.AddSingleton<IJobLifecycleService, JobLifecycleService>();
    builder.Services.AddSingleton<ActiveJobTracker>();

    // NEX-138: Command queue and handler dispatch
    builder.Services.AddSingleton<CommandQueue>();
    builder.Services.AddSingleton<JobAssignHandler>();
    builder.Services.AddSingleton<JobCancelHandler>();
    builder.Services.AddSingleton<CommandHandlerRegistry>(sp =>
    {
        var registry = new CommandHandlerRegistry();
        registry.Register(sp.GetRequiredService<JobAssignHandler>());
        registry.Register(sp.GetRequiredService<JobCancelHandler>());
        return registry;
    });
    builder.Services.AddHostedService<CommandQueueWorker>();

    // Wire SignalR events → command queue (must register handlers BEFORE connection starts)
    builder.Services.AddHostedService<SignalRCommandBridge>();

    // NEX-134: Heartbeat (registers ack handler before connection)
    builder.Services.AddSingleton<ResourceMonitor>();
    builder.Services.AddHostedService<HeartbeatWorker>();

    // NEX-34: Job timeout monitor
    builder.Services.AddHostedService<JobTimeoutMonitor>();

    // Hub connection worker starts AFTER all handlers are registered
    builder.Services.AddHostedService<HubConnectionWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Spoke terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
