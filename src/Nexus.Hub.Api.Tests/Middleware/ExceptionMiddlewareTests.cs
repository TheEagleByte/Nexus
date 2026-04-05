using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Middleware;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Exceptions;
using NotFoundException = Nexus.Hub.Domain.Exceptions.NotFoundException;
using ValidationException = Nexus.Hub.Domain.Exceptions.ValidationException;

namespace Nexus.Hub.Api.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionMiddleware>> _logger = new();

    private ExceptionMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, _logger.Object);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrorResponse?> ReadErrorResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ErrorResponse>(context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404()
    {
        var middleware = CreateMiddleware(_ => throw new NotFoundException("Spoke not found"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
        var body = await ReadErrorResponse(context);
        Assert.Equal("NOT_FOUND", body!.Error.Code);
        Assert.Equal("Spoke not found", body.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns422()
    {
        var middleware = CreateMiddleware(_ => throw new ValidationException("Invalid input"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        var body = await ReadErrorResponse(context);
        Assert.Equal("VALIDATION_ERROR", body!.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_ConflictException_Returns409()
    {
        var middleware = CreateMiddleware(_ => throw new ConflictException("Already exists"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        var body = await ReadErrorResponse(context);
        Assert.Equal("CONFLICT", body!.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_Returns400()
    {
        var middleware = CreateMiddleware(_ => throw new DomainException("Bad request", "CUSTOM_CODE"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        var body = await ReadErrorResponse(context);
        Assert.Equal("CUSTOM_CODE", body!.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500_HidesMessage()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("secret info"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        var body = await ReadErrorResponse(context);
        Assert.Equal("INTERNAL_ERROR", body!.Error.Code);
        Assert.Equal("An unexpected error occurred", body.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_SetsJsonContentType()
    {
        var middleware = CreateMiddleware(_ => throw new NotFoundException("test"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationId()
    {
        var middleware = CreateMiddleware(_ => throw new NotFoundException("test"));
        var context = CreateHttpContext();
        context.TraceIdentifier = "test-correlation-123";

        await middleware.InvokeAsync(context);

        var body = await ReadErrorResponse(context);
        Assert.Equal("test-correlation-123", body!.Error.CorrelationId);
    }
}
