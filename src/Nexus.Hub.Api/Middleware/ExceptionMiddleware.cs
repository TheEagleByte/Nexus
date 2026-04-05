using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Exceptions;

namespace Nexus.Hub.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionMiddleware> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "VALIDATION_ERROR"),
            ConflictException => (StatusCodes.Status409Conflict, "CONFLICT"),
            DomainException domainEx => (StatusCodes.Status400BadRequest, domainEx.Code),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")
        };

        if (statusCode >= 500)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Domain exception ({Code}): {Message}", errorCode, exception.Message);

        var response = new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = errorCode,
                Message = statusCode >= 500
                    ? "An unexpected error occurred"
                    : exception.Message,
                Status = statusCode,
                CorrelationId = context.TraceIdentifier
            }
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response, JsonOptions);
    }
}
