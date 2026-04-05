using System.Threading.Channels;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class CommandQueue
{
    private readonly Channel<CommandEnvelope> _channel = Channel.CreateBounded<CommandEnvelope>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(command, cancellationToken);

    public IAsyncEnumerable<CommandEnvelope> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public int Count => _channel.Reader.Count;
}
