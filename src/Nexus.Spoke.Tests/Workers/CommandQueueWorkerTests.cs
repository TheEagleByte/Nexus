using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;

namespace Nexus.Spoke.Tests.Workers;

public class CommandQueueWorkerTests
{
    [Fact]
    public async Task ProcessesCommand_WithCorrectHandler()
    {
        var queue = new CommandQueue();
        var registry = new CommandHandlerRegistry();
        var handler = new Mock<ICommandHandler>();
        handler.Setup(h => h.CommandType).Returns("job.assign");
        registry.Register(handler.Object);

        var worker = new CommandQueueWorker(queue, registry, NullLogger<CommandQueueWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);

        var command = new CommandEnvelope("job.assign", "test-payload", DateTimeOffset.UtcNow);
        await queue.EnqueueAsync(command);

        await Task.Delay(200); // Allow processing
        await worker.StopAsync(CancellationToken.None);

        handler.Verify(h => h.HandleAsync(
            It.Is<CommandEnvelope>(c => c.CommandType == "job.assign"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownCommandType_LogsWarning_DoesNotThrow()
    {
        var queue = new CommandQueue();
        var registry = new CommandHandlerRegistry();
        var worker = new CommandQueueWorker(queue, registry, NullLogger<CommandQueueWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);

        await queue.EnqueueAsync(new CommandEnvelope("unknown.type", "payload", DateTimeOffset.UtcNow));

        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // Worker didn't crash — no assertion needed beyond reaching this point
    }

    [Fact]
    public async Task HandlerThrows_WorkerContinuesProcessing()
    {
        var queue = new CommandQueue();
        var registry = new CommandHandlerRegistry();

        var failingHandler = new Mock<ICommandHandler>();
        failingHandler.Setup(h => h.CommandType).Returns("failing");
        failingHandler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler error"));
        registry.Register(failingHandler.Object);

        var successHandler = new Mock<ICommandHandler>();
        successHandler.Setup(h => h.CommandType).Returns("success");
        registry.Register(successHandler.Object);

        var worker = new CommandQueueWorker(queue, registry, NullLogger<CommandQueueWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);

        await queue.EnqueueAsync(new CommandEnvelope("failing", "payload", DateTimeOffset.UtcNow));
        await queue.EnqueueAsync(new CommandEnvelope("success", "payload", DateTimeOffset.UtcNow));

        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);

        // Second handler was still called despite first one throwing
        successHandler.Verify(h => h.HandleAsync(
            It.Is<CommandEnvelope>(c => c.CommandType == "success"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
