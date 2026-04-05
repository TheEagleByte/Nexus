namespace Nexus.Spoke.Handlers;

public class CommandHandlerRegistry
{
    private readonly Dictionary<string, ICommandHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICommandHandler handler)
    {
        _handlers[handler.CommandType] = handler;
    }

    public ICommandHandler? GetHandler(string commandType)
    {
        _handlers.TryGetValue(commandType, out var handler);
        return handler;
    }

    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys.ToList().AsReadOnly();
}
