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

        var createdResult = Assert.IsType<CreatedResult>(result);
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

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        var errorResponse = Assert.IsType<ErrorResponse>(unauthorizedResult.Value);
        Assert.Equal("UNAUTHORIZED", errorResponse.Error.Code);
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

        Assert.IsType<CreatedResult>(result);
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

    [Fact]
    public async Task ListAsync_ReturnsSpokesWithPagination()
    {
        var now = DateTimeOffset.UtcNow;
        var capabilities = JsonSerializer.SerializeToDocument(new[] { "code" });
        var config = JsonSerializer.SerializeToDocument(new { });

        var spokes = new List<Spoke>
        {
            new()
            {
                Id = Guid.NewGuid(), Name = "spoke-1", Status = SpokeStatus.Online,
                Capabilities = capabilities, Config = config,
                LastSeen = now, CreatedAt = now, UpdatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(), Name = "spoke-2", Status = SpokeStatus.Offline,
                Capabilities = capabilities, Config = config,
                LastSeen = now, CreatedAt = now, UpdatedAt = now
            }
        };

        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spokes);
        _spokeServiceMock
            .Setup(s => s.GetSpokeCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _controller.ListAsync(null, 50, 0, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpokeListResponse>(okResult.Value);
        Assert.Equal(2, response.Spokes.Count);
        Assert.Equal(2, response.Total);
        Assert.Equal(50, response.Limit);
        Assert.Equal(0, response.Offset);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_PassesStatusToService()
    {
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(SpokeStatus.Online, 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spokeServiceMock
            .Setup(s => s.GetSpokeCountAsync(SpokeStatus.Online, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(SpokeStatus.Online, 10, 5, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpokeListResponse>(okResult.Value);
        Assert.Empty(response.Spokes);
        Assert.Equal(0, response.Total);
        Assert.Equal(10, response.Limit);
        Assert.Equal(5, response.Offset);

        _spokeServiceMock.Verify(s => s.ListSpokesAsync(SpokeStatus.Online, 10, 5, It.IsAny<CancellationToken>()), Times.Once);
        _spokeServiceMock.Verify(s => s.GetSpokeCountAsync(SpokeStatus.Online, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_NegativeOffset_Returns400()
    {
        var result = await _controller.ListAsync(null, 50, -1, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ZeroLimit_Returns400()
    {
        var result = await _controller.ListAsync(null, 0, 0, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ListAsync_LimitCappedAt100()
    {
        _spokeServiceMock
            .Setup(s => s.ListSpokesAsync(null, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _spokeServiceMock
            .Setup(s => s.GetSpokeCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(null, 500, 0, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpokeListResponse>(okResult.Value);
        Assert.Equal(100, response.Limit);

        _spokeServiceMock.Verify(s => s.ListSpokesAsync(null, 100, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ExistingSpoke_ReturnsSpokeDetail()
    {
        var now = DateTimeOffset.UtcNow;
        var spokeId = Guid.NewGuid();
        var capabilities = JsonSerializer.SerializeToDocument(new[] { "code", "test" });
        var config = JsonSerializer.SerializeToDocument(new { maxJobs = 3 });
        var profile = JsonSerializer.SerializeToDocument(new { os = "linux" });

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Spoke
            {
                Id = spokeId,
                Name = "test-spoke",
                Status = SpokeStatus.Online,
                Capabilities = capabilities,
                Config = config,
                Profile = profile,
                LastSeen = now,
                CreatedAt = now,
                UpdatedAt = now
            });

        var result = await _controller.GetAsync(spokeId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpokeDetailResponse>(okResult.Value);
        Assert.Equal(spokeId, response.Id);
        Assert.Equal("test-spoke", response.Name);
        Assert.Equal(SpokeStatus.Online, response.Status);
        Assert.Equal(now, response.RegisteredAt);
        Assert.NotNull(response.Profile);
    }

    [Fact]
    public async Task GetAsync_NonExistentSpoke_ThrowsNotFoundException()
    {
        var spokeId = Guid.NewGuid();

        _spokeServiceMock
            .Setup(s => s.GetSpokeAsync(spokeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Nexus.Hub.Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found"));

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _controller.GetAsync(spokeId, CancellationToken.None));
    }
}
