using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Middleware;
using Nexus.Hub.Infrastructure;
using Nexus.Hub.Infrastructure.Data;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        });

    builder.Services.AddDbContext<NexusDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddInfrastructure();

    builder.Services.AddSignalR();
    builder.Services.AddHostedService<SpokeTimeoutService>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LocalOnly", policy =>
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    // Authentication stub - Google OAuth will be configured in a future ticket
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.Database.Migrate();
    }

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseCors("LocalOnly");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<NexusHub>("/hubs/nexus");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
