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

    // NEX-138: Command queue and handler dispatch
    builder.Services.AddSingleton<CommandQueue>();
    builder.Services.AddSingleton<CommandHandlerRegistry>(sp =>
    {
        var registry = new CommandHandlerRegistry();
        // Future handlers will be registered here via DI scan
        return registry;
    });
    builder.Services.AddHostedService<CommandQueueWorker>();

    // Wire SignalR events → command queue (must register handlers BEFORE connection starts)
    builder.Services.AddHostedService<SignalRCommandBridge>();

    // NEX-134: Heartbeat (registers ack handler before connection)
    builder.Services.AddSingleton<ResourceMonitor>();
    builder.Services.AddHostedService<HeartbeatWorker>();

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
