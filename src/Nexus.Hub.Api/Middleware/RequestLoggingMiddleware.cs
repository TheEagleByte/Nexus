using System.Diagnostics;

namespace Nexus.Hub.Api.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString;

        _logger.LogInformation("Request started: {Method} {Path}{QueryString}",
            method, path, queryString);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;

            _logger.LogInformation("Request completed: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                method, path, statusCode, stopwatch.ElapsedMilliseconds);
        }
    }
}
