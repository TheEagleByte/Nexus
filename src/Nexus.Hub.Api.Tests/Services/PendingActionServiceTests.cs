using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Exceptions;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class PendingActionServiceTests
{
    private readonly Mock<IPendingActionRepository> _repoMock = new();
    private readonly Mock<ILogger<PendingActionService>> _loggerMock = new();
    private readonly PendingActionService _service;

    public PendingActionServiceTests()
    {
        _service = new PendingActionService(_repoMock.Object, _loggerMock.Object);
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_SetsDefaults_ReturnsAction()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAction a, CancellationToken _) => a);

        var result = await _service.CreateAsync(spokeId, projectId, jobId, PendingActionType.PlanReview, 5);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(spokeId, result.SpokeId);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(PendingActionType.PlanReview, result.Type);
        Assert.Equal(PendingActionStatus.Pending, result.Status);
        Assert.Equal(5, result.Priority);
        Assert.True(result.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Null(result.ResolvedAt);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithMetadata_PassesThrough()
    {
        var metadata = JsonDocument.Parse("{\"summary\":\"test plan\"}");

        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAction a, CancellationToken _) => a);

        var result = await _service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PreExecution, metadata: metadata);

        Assert.NotNull(result.Metadata);
    }

    #endregion

    #region GetAsync

    [Fact]
    public async Task GetAsync_Exists_ReturnsAction()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending };

        _repoMock
            .Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        var result = await _service.GetAsync(actionId);

        Assert.NotNull(result);
        Assert.Equal(actionId, result!.Id);
    }

    [Fact]
    public async Task GetAsync_NotFound_ThrowsNotFoundException()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid()));
    }

    #endregion

    #region ResolveAsync

    [Fact]
    public async Task ResolveAsync_Approve_SetsApprovedStatus()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction
        {
            Id = actionId,
            SpokeId = Guid.NewGuid(),
            Status = PendingActionStatus.Pending,
            Metadata = JsonDocument.Parse("{\"jobId\":\"test\"}")
        };

        _repoMock.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.ResolveAsync(actionId, "approve", "LGTM");

        Assert.Equal(PendingActionStatus.Approved, result.Status);
        Assert.NotNull(result.ResolvedAt);
        Assert.Contains("notes", result.Metadata!.RootElement.EnumerateObject().Select(p => p.Name));
        _repoMock.Verify(r => r.UpdateAsync(action, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_Reject_SetsRejectedStatus()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending };

        _repoMock.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.ResolveAsync(actionId, "reject");

        Assert.Equal(PendingActionStatus.Rejected, result.Status);
        Assert.NotNull(result.ResolvedAt);
    }

    [Fact]
    public async Task ResolveAsync_Respond_SetsResolvedStatus()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending };

        _repoMock.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.ResolveAsync(actionId, "respond", "Here's the answer");

        Assert.Equal(PendingActionStatus.Resolved, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_AlreadyResolved_ThrowsConflict()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Approved };

        _repoMock.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await Assert.ThrowsAsync<ConflictException>(() => _service.ResolveAsync(actionId, "approve"));
    }

    [Fact]
    public async Task ResolveAsync_NotFound_ThrowsNotFoundException()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ResolveAsync(Guid.NewGuid(), "approve"));
    }

    [Fact]
    public async Task ResolveAsync_InvalidAction_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ResolveAsync(Guid.NewGuid(), "invalid"));
    }

    [Fact]
    public async Task ResolveAsync_WithModifications_MergesIntoMetadata()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction
        {
            Id = actionId,
            Status = PendingActionStatus.Pending,
            Metadata = JsonDocument.Parse("{\"summary\":\"original\"}")
        };
        var modifications = JsonDocument.Parse("{\"changes\":[\"step 3 removed\"]}");

        _repoMock.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<PendingAction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _service.ResolveAsync(actionId, "approve", "Modified plan", modifications);

        var root = result.Metadata!.RootElement;
        Assert.True(root.TryGetProperty("notes", out _));
        Assert.True(root.TryGetProperty("modifications", out _));
        Assert.True(root.TryGetProperty("summary", out _));
    }

    #endregion

    #region ListAsync / CountAsync

    [Fact]
    public async Task ListAsync_DelegatesToRepo()
    {
        var spokeId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.ListAsync(spokeId, null, PendingActionType.PlanReview, null, 25, 10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingAction>());

        var result = await _service.ListAsync(spokeId, type: PendingActionType.PlanReview, limit: 25, offset: 10);

        Assert.Empty(result);
        _repoMock.Verify(r => r.ListAsync(spokeId, null, PendingActionType.PlanReview, null, 25, 10, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountAsync_DelegatesToRepo()
    {
        _repoMock
            .Setup(r => r.CountAsync(null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _service.CountAsync();

        Assert.Equal(42, result);
    }

    #endregion
}
