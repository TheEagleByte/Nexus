using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class HubConnectionServiceTests
{
    private static HubConnectionService CreateService(SpokeConfiguration? config = null)
    {
        config ??= new SpokeConfiguration
        {
            Spoke = new SpokeConfiguration.SpokeIdentityConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Spoke"
            },
            Hub = new SpokeConfiguration.HubConnectionConfig
            {
                Url = "wss://hub.test/api/hub",
                Token = "test-token"
            }
        };

        return new HubConnectionService(
            Options.Create(config),
            NullLogger<HubConnectionService>.Instance);
    }

    [Fact]
    public void IsConnected_WhenNotStarted_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void SpokeId_ParsesFromConfig()
    {
        var id = Guid.NewGuid();
        var config = new SpokeConfiguration
        {
            Spoke = new SpokeConfiguration.SpokeIdentityConfig { Id = id.ToString(), Name = "Test" },
            Hub = new SpokeConfiguration.HubConnectionConfig { Url = "wss://hub.test/api/hub", Token = "token" }
        };

        var service = CreateService(config);
        Assert.Equal(id, service.SpokeId);
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_DoesNotThrow()
    {
        var service = CreateService();
        // Should log warning but not throw
        await service.SendAsync("TestMethod", new { Data = "test" });
    }

    [Fact]
    public void OnReceived_StoresRegistration()
    {
        var service = CreateService();
        var called = false;

        service.OnReceived<string>("TestMethod", _ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // Handler is stored but not yet called (no connection)
        Assert.False(called);
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var service = CreateService();
        await service.DisposeAsync();
    }
}
