using Moq;
using Nexus.Spoke.Handlers;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Tests.Handlers;

public class CommandHandlerRegistryTests
{
    [Fact]
    public void Register_AndRetrieve_ReturnsHandler()
    {
        var registry = new CommandHandlerRegistry();
        var handler = CreateMockHandler("job.assign");

        registry.Register(handler);

        Assert.Same(handler, registry.GetHandler("job.assign"));
    }

    [Fact]
    public void GetHandler_UnknownType_ReturnsNull()
    {
        var registry = new CommandHandlerRegistry();
        Assert.Null(registry.GetHandler("unknown.command"));
    }

    [Fact]
    public void GetHandler_IsCaseInsensitive()
    {
        var registry = new CommandHandlerRegistry();
        var handler = CreateMockHandler("Job.Assign");

        registry.Register(handler);

        Assert.Same(handler, registry.GetHandler("job.assign"));
        Assert.Same(handler, registry.GetHandler("JOB.ASSIGN"));
    }

    [Fact]
    public void Register_SameType_OverwritesPrevious()
    {
        var registry = new CommandHandlerRegistry();
        var handler1 = CreateMockHandler("job.assign");
        var handler2 = CreateMockHandler("job.assign");

        registry.Register(handler1);
        registry.Register(handler2);

        Assert.Same(handler2, registry.GetHandler("job.assign"));
    }

    [Fact]
    public void RegisteredTypes_ReflectsRegisteredHandlers()
    {
        var registry = new CommandHandlerRegistry();
        registry.Register(CreateMockHandler("job.assign"));
        registry.Register(CreateMockHandler("message.to_spoke"));

        var types = registry.RegisteredTypes;
        Assert.Equal(2, types.Count);
        Assert.Contains("job.assign", types, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("message.to_spoke", types, StringComparer.OrdinalIgnoreCase);
    }

    private static ICommandHandler CreateMockHandler(string commandType)
    {
        var mock = new Mock<ICommandHandler>();
        mock.Setup(h => h.CommandType).Returns(commandType);
        return mock.Object;
    }
}
