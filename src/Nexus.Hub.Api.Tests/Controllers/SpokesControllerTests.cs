using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class SpokesControllerTests
{
    private readonly Mock<ISpokeService> _spokeServiceMock = new();
    private readonly SpokesController _controller;
    private const string ValidPsk = "test-psk";

    public SpokesControllerTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Spoke:PreSharedKey"] = ValidPsk
            })
            .Build();

        _controller = new SpokesController(_spokeServiceMock.Object, config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_Returns201WithSpokeDetail()
    {
        var now = DateTimeOffset.UtcNow;
        var spokeId = Guid.NewGuid();
        var capabilities = JsonSerializer.SerializeToDocument(new[] { "code", "test" });
        var config = JsonSerializer.SerializeToDocument(new { maxJobs = 5 });

        _spokeServiceMock
            .Setup(s => s.RegisterSpokeAsync(
                It.IsAny<string>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<JsonDocument?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId,
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = capabilities,
                Config = config,
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        var request = new SpokeRegistrationRequest
        {
            Psk = ValidPsk,
            Name = "test-spoke",
            Capabilities = ["code", "test"],
            Os = "linux",
            Architecture = "x64"
        };

        var result = await _controller.RegisterAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var response = Assert.IsType<SpokeDetailResponse>(createdResult.Value);
        Assert.Equal(spokeId, response.Id);
        Assert.Equal("test-spoke", response.Name);
        Assert.Equal(SpokeStatus.Online, response.Status);
        Assert.Equal(now, response.RegisteredAt);
    }

    [Fact]
    public async Task RegisterAsync_InvalidPsk_Returns401()
    {
        var request = new SpokeRegistrationRequest
        {
            Psk = "wrong-key",
            Name = "test-spoke"
        };

        var result = await _controller.RegisterAsync(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);

        var errorResponse = Assert.IsType<ErrorResponse>(unauthorizedResult.Value);
        Assert.Equal("UNAUTHORIZED", errorResponse.Error.Code);
    }

    [Fact]
    public async Task RegisterAsync_EmptyPsk_Returns401()
    {
        var request = new SpokeRegistrationRequest
        {
            Psk = "",
            Name = "test-spoke"
        };

        var result = await _controller.RegisterAsync(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RegisterAsync_WithProfile_PassesProfileToService()
    {
        var now = DateTimeOffset.UtcNow;
        var capabilities = JsonSerializer.SerializeToDocument(Array.Empty<string>());
        var config = JsonSerializer.SerializeToDocument(new { });

        _spokeServiceMock
            .Setup(s => s.RegisterSpokeAsync(
                It.IsAny<string>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<JsonDocument>(),
                It.Is<JsonDocument?>(p => p != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = Guid.NewGuid(),
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = capabilities,
                Config = config,
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        var request = new SpokeRegistrationRequest
        {
            Psk = ValidPsk,
            Name = "test-spoke",
            Os = "windows",
            Architecture = "arm64"
        };

        var result = await _controller.RegisterAsync(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        _spokeServiceMock.Verify(s => s.RegisterSpokeAsync(
            "test-spoke",
            It.IsAny<JsonDocument>(),
            It.IsAny<JsonDocument>(),
            It.Is<JsonDocument?>(p => p != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_NoProfileFields_PassesNullProfile()
    {
        var now = DateTimeOffset.UtcNow;
        var capabilities = JsonSerializer.SerializeToDocument(Array.Empty<string>());
        var config = JsonSerializer.SerializeToDocument(new { });

        _spokeServiceMock
            .Setup(s => s.RegisterSpokeAsync(
                It.IsAny<string>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<JsonDocument?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = Guid.NewGuid(),
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = capabilities,
                Config = config,
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        var request = new SpokeRegistrationRequest
        {
            Psk = ValidPsk,
            Name = "test-spoke"
        };

        await _controller.RegisterAsync(request, CancellationToken.None);

        _spokeServiceMock.Verify(s => s.RegisterSpokeAsync(
            "test-spoke",
            It.IsAny<JsonDocument>(),
            It.IsAny<JsonDocument>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
