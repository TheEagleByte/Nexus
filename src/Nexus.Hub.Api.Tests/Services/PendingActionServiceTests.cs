using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Api.Tests.Services;

public class PendingActionServiceTests
{
    private readonly Mock<IPendingActionRepository> _repo = new();
    private readonly Mock<ILogger<PendingActionService>> _logger = new();
    private readonly PendingActionService _sut;

    public PendingActionServiceTests()
    {
        _sut = new PendingActionService(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateAsync_SetsStatusToPending()
    {
        var result = await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PlanReview, 1);

        Assert.Equal(PendingActionStatus.Pending, result.Status);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAtAutomatically()
    {
        var before = DateTimeOffset.UtcNow;

        var result = await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PlanReview, 1);

        Assert.True(result.CreatedAt >= before);
        Assert.True(result.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueId()
    {
        var action1 = await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PlanReview, 1);
        var action2 = await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PlanReview, 1);

        Assert.NotEqual(action1.Id, action2.Id);
    }

    [Fact]
    public async Task CreateAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await _sut.CreateAsync(spokeId, projectId, jobId, PendingActionType.PreExecution, 5);

        _repo.Verify(r => r.AddAsync(It.Is<PendingAction>(pa =>
            pa.SpokeId == spokeId &&
            pa.ProjectId == projectId &&
            pa.JobId == jobId &&
            pa.Type == PendingActionType.PreExecution &&
            pa.Priority == 5), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithMetadata_SetsMetadata()
    {
        var metadata = JsonDocument.Parse("{\"ticket\":\"NEX-23\"}");

        var result = await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PendingActionType.PlanReview, 1, metadata);

        Assert.NotNull(result.Metadata);
    }

    [Fact]
    public async Task ResolveAsync_Approved_SetsStatusAndTimestamp()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _sut.ResolveAsync(actionId, approved: true);

        Assert.Equal(PendingActionStatus.Approved, action.Status);
        Assert.NotNull(action.ResolvedAt);
        _repo.Verify(r => r.UpdateAsync(action, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_Rejected_SetsStatusAndTimestamp()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _sut.ResolveAsync(actionId, approved: false);

        Assert.Equal(PendingActionStatus.Rejected, action.Status);
        Assert.NotNull(action.ResolvedAt);
    }

    [Fact]
    public async Task ResolveAsync_WithResolution_SetsMetadata()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _sut.ResolveAsync(actionId, approved: true, resolution: "Looks good");

        Assert.NotNull(action.Metadata);
        var root = action.Metadata.RootElement;
        Assert.Equal("Looks good", root.GetProperty("resolution").GetString());
    }

    [Fact]
    public async Task ResolveAsync_WithResolution_PreservesExistingMetadata()
    {
        var actionId = Guid.NewGuid();
        var existingMetadata = JsonDocument.Parse("{\"ticket\":\"NEX-23\",\"context\":\"plan review\"}");
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, Metadata = existingMetadata };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _sut.ResolveAsync(actionId, approved: true, resolution: "Looks good");

        Assert.NotNull(action.Metadata);
        var root = action.Metadata.RootElement;
        Assert.Equal("NEX-23", root.GetProperty("ticket").GetString());
        Assert.Equal("plan review", root.GetProperty("context").GetString());
        Assert.Equal("Looks good", root.GetProperty("resolution").GetString());
    }

    [Fact]
    public async Task ResolveAsync_AlreadyResolved_ThrowsValidationException()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Approved, CreatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.ValidationException>(
            () => _sut.ResolveAsync(actionId, approved: true));
    }

    [Fact]
    public async Task ResolveAsync_NotFound_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PendingAction?)null);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.ResolveAsync(Guid.NewGuid(), approved: true));
    }

    [Fact]
    public async Task GetAsync_ExistingAction_ReturnsAction()
    {
        var actionId = Guid.NewGuid();
        var action = new PendingAction { Id = actionId, Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        _repo.Setup(r => r.GetByIdAsync(actionId, It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var result = await _sut.GetAsync(actionId);

        Assert.Equal(actionId, result.Id);
    }

    [Fact]
    public async Task GetAsync_Missing_ThrowsNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PendingAction?)null);

        await Assert.ThrowsAsync<Nexus.Hub.Domain.Exceptions.NotFoundException>(
            () => _sut.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListBySpokeAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        var actions = new List<PendingAction> { new() { Id = Guid.NewGuid(), Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow } };
        _repo.Setup(r => r.ListBySpokeAsync(spokeId, PendingActionStatus.Pending, 50, 0, It.IsAny<CancellationToken>())).ReturnsAsync(actions);

        var result = await _sut.ListBySpokeAsync(spokeId, PendingActionStatus.Pending);

        Assert.Single(result);
        _repo.Verify(r => r.ListBySpokeAsync(spokeId, PendingActionStatus.Pending, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAllAsync_DelegatesToRepository()
    {
        var actions = new List<PendingAction> { new() { Id = Guid.NewGuid(), Status = PendingActionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow } };
        _repo.Setup(r => r.ListAsync(null, PendingActionStatus.Pending, null, null, 50, 0, It.IsAny<CancellationToken>())).ReturnsAsync(actions);

        var result = await _sut.ListAllAsync(status: PendingActionStatus.Pending);

        Assert.Single(result);
        _repo.Verify(r => r.ListAsync(null, PendingActionStatus.Pending, null, null, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountAsync_DelegatesToRepository()
    {
        var spokeId = Guid.NewGuid();
        _repo.Setup(r => r.CountAsync(spokeId, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await _sut.CountAsync(spokeId: spokeId);

        Assert.Equal(7, result);
    }
}
