using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class CommandQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_MaintainsFIFOOrder()
    {
        var queue = new CommandQueue();
        var cmd1 = new CommandEnvelope("type1", "payload1", DateTimeOffset.UtcNow);
        var cmd2 = new CommandEnvelope("type2", "payload2", DateTimeOffset.UtcNow);
        var cmd3 = new CommandEnvelope("type3", "payload3", DateTimeOffset.UtcNow);

        await queue.EnqueueAsync(cmd1);
        await queue.EnqueueAsync(cmd2);
        await queue.EnqueueAsync(cmd3);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var results = new List<CommandEnvelope>();

        await foreach (var item in queue.DequeueAllAsync(cts.Token))
        {
            results.Add(item);
            if (results.Count == 3) break;
        }

        Assert.Equal("type1", results[0].CommandType);
        Assert.Equal("type2", results[1].CommandType);
        Assert.Equal("type3", results[2].CommandType);
    }

    [Fact]
    public async Task Count_ReflectsQueueState()
    {
        var queue = new CommandQueue();
        Assert.Equal(0, queue.Count);

        await queue.EnqueueAsync(new CommandEnvelope("test", "payload", DateTimeOffset.UtcNow));
        Assert.Equal(1, queue.Count);

        await queue.EnqueueAsync(new CommandEnvelope("test2", "payload2", DateTimeOffset.UtcNow));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public async Task ConcurrentEnqueue_NoItemsLost()
    {
        var queue = new CommandQueue();
        const int itemCount = 100;

        var tasks = Enumerable.Range(0, itemCount).Select(i =>
            queue.EnqueueAsync(new CommandEnvelope($"type-{i}", $"payload-{i}", DateTimeOffset.UtcNow)).AsTask()
        ).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(itemCount, queue.Count);
    }
}
