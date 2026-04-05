using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class ResourceMonitorTests
{
    [Fact]
    public void GetCurrentUsage_ReturnsPositiveMemory()
    {
        var monitor = new ResourceMonitor();
        var usage = monitor.GetCurrentUsage();
        Assert.True(usage.MemoryUsageMb > 0);
    }

    [Fact]
    public void GetCurrentUsage_ReturnsNonNegativeDisk()
    {
        var monitor = new ResourceMonitor();
        var usage = monitor.GetCurrentUsage();
        Assert.True(usage.DiskUsageMb >= 0);
    }

    [Fact]
    public void GetCurrentUsage_CpuIsZero_Placeholder()
    {
        var monitor = new ResourceMonitor();
        var usage = monitor.GetCurrentUsage();
        Assert.Equal(0.0, usage.CpuUsagePercent);
    }
}
