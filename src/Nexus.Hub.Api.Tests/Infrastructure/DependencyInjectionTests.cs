using Microsoft.Extensions.DependencyInjection;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;
using Nexus.Hub.Infrastructure;

namespace Nexus.Hub.Api.Tests.Infrastructure;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersAllServices()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        // Verify service registrations exist
        Assert.Contains(services, s => s.ServiceType == typeof(ISpokeService));
        Assert.Contains(services, s => s.ServiceType == typeof(IProjectService));
        Assert.Contains(services, s => s.ServiceType == typeof(IJobService));
        Assert.Contains(services, s => s.ServiceType == typeof(IMessageService));
    }

    [Fact]
    public void AddInfrastructure_RegistersAllRepositories()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        Assert.Contains(services, s => s.ServiceType == typeof(ISpokeRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IProjectRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IJobRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IMessageRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IOutputStreamRepository));
    }

    [Fact]
    public void AddInfrastructure_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddInfrastructure();

        Assert.Same(services, result);
    }
}
