using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Api.Controllers;
using Nexus.Hub.Api.Hubs;
using Nexus.Hub.Api.Models;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Api.Tests.Controllers;

public class PendingActionsControllerTests
{
    private readonly Mock<IPendingActionService> _serviceMock = new();
    private readonly Mock<IHubContext<NexusHub>> _hubContextMock = new();
    private readonly Mock<ILogger<PendingActionsController>> _loggerMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _allClientsMock = new();
    private readonly PendingActionsController _controller;

    public PendingActionsControllerTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);

        _controller = new PendingActionsController(_serviceMock.Object, _hubContextMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region ListAsync

    [Fact]
    public async Task ListAsync_ReturnsPaginatedResults()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddMinutes(-30);

        var actions = new List<PendingAction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SpokeId = spokeId,
                ProjectId = projectId,
                JobId = jobId,
                Type = PendingActionType.PlanReview,
                Status = PendingActionStatus.Pending,
                Priority = 1,
                CreatedAt = now,
                Metadata = JsonDocument.Parse("{\"summary\":\"Test plan\",\"description\":\"Plan details\"}"),
                Spoke = new Spoke { Id = spokeId, Name = "Test Spoke" },
                Project = new Project { Id = projectId, ExternalKey = "PROJ-123", Name = "Test" },
                Job = new Job { Id = jobId, Type = JobType.Implement }
            }
        };

        _serviceMock
            .Setup(s => s.ListAsync(null, null, null, PendingActionStatus.Pending, 50, 0, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);
        _serviceMock
            .Setup(s => s.CountAsync(null, null, null, PendingActionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _controller.ListAsync(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PendingActionListResponse>(ok.Value);
        Assert.Single(response.PendingActions);
        Assert.Equal(1, response.Total);
        Assert.Equal(50, response.Limit);
        Assert.Equal(0, response.Offset);

        var action = response.PendingActions[0];
        Assert.Equal("Test Spoke", action.SpokeName);
        Assert.Equal("PROJ-123", action.ExternalKey);
        Assert.Equal("Test plan", action.Summary);
        Assert.Equal("Plan details", action.Description);
        Assert.Contains("m", action.Age);
    }

    [Fact]
    public async Task ListAsync_WithFilters_PassesThrough()
    {
        var spokeId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.ListAsync(spokeId, null, PendingActionType.PrReview, PendingActionStatus.Pending, 25, 5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingAction>());
        _serviceMock
            .Setup(s => s.CountAsync(spokeId, null, PendingActionType.PrReview, PendingActionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(gateType: PendingActionType.PrReview, spokeId: spokeId, limit: 25, offset: 5, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PendingActionListResponse>(ok.Value);
        Assert.Empty(response.PendingActions);
        Assert.Equal(25, response.Limit);
        Assert.Equal(5, response.Offset);
    }

    [Fact]
    public async Task ListAsync_AgeAscSort_PassesSortDescending()
    {
        _serviceMock
            .Setup(s => s.ListAsync(null, null, null, PendingActionStatus.Pending, 50, 0, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingAction>());
        _serviceMock
            .Setup(s => s.CountAsync(null, null, null, PendingActionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _controller.ListAsync(sort: "age_asc", cancellationToken: CancellationToken.None);

        _serviceMock.Verify(s => s.ListAsync(null, null, null, PendingActionStatus.Pending, 50, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_NegativeOffset_Returns400()
    {
        var result = await _controller.ListAsync(offset: -1, cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ClampsLimit()
    {
        _serviceMock
            .Setup(s => s.ListAsync(null, null, null, PendingActionStatus.Pending, 100, 0, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingAction>());
        _serviceMock
            .Setup(s => s.CountAsync(null, null, null, PendingActionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _controller.ListAsync(limit: 999, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PendingActionListResponse>(ok.Value);
        Assert.Equal(100, response.Limit);
    }

    #endregion

    #region ResolveAsync

    [Fact]
    public async Task ResolveAsync_ValidRequest_Returns200()
    {
        var actionId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();
        var resolvedAt = DateTimeOffset.UtcNow;

        _serviceMock
            .Setup(s => s.ResolveAsync(actionId, "approve", "LGTM", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingAction
            {
                Id = actionId,
                SpokeId = spokeId,
                Status = PendingActionStatus.Approved,
                ResolvedAt = resolvedAt,
                Metadata = JsonDocument.Parse("{\"notes\":\"LGTM\"}")
            });

        var request = new ResolvePendingActionRequest { Action = "approve", Notes = "LGTM" };
        var result = await _controller.ResolveAsync(actionId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ResolvePendingActionResponse>(ok.Value);
        Assert.Equal(actionId, response.Id);
        Assert.Equal(PendingActionStatus.Approved, response.Status);
        Assert.Equal("approve", response.Action);
        Assert.Equal(resolvedAt, response.ResolvedAt);
    }

    [Fact]
    public async Task ResolveAsync_InvalidAction_Returns400()
    {
        var request = new ResolvePendingActionRequest { Action = "invalid" };
        var result = await _controller.ResolveAsync(Guid.NewGuid(), request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_REQUEST", error.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_BroadcastsSignalREvent()
    {
        var actionId = Guid.NewGuid();
        var spokeId = Guid.NewGuid();

        _serviceMock
            .Setup(s => s.ResolveAsync(actionId, "reject", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingAction
            {
                Id = actionId,
                SpokeId = spokeId,
                Status = PendingActionStatus.Rejected,
                ResolvedAt = DateTimeOffset.UtcNow
            });

        var request = new ResolvePendingActionRequest { Action = "reject" };
        await _controller.ResolveAsync(actionId, request, CancellationToken.None);

        _allClientsMock.Verify(
            c => c.SendCoreAsync("PendingActionResolved", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ActionNotFound_ThrowsNotFoundException()
    {
        var actionId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.ResolveAsync(actionId, "approve", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"PendingAction {actionId} not found"));

        var request = new ResolvePendingActionRequest { Action = "approve" };
        await Assert.ThrowsAsync<NotFoundException>(
            () => _controller.ResolveAsync(actionId, request, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_AlreadyResolved_ThrowsConflictException()
    {
        var actionId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.ResolveAsync(actionId, "approve", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException($"PendingAction {actionId} is already resolved"));

        var request = new ResolvePendingActionRequest { Action = "approve" };
        await Assert.ThrowsAsync<ConflictException>(
            () => _controller.ResolveAsync(actionId, request, CancellationToken.None));
    }

    #endregion
}
