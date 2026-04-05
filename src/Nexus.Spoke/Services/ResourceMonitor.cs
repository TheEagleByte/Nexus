using System.Diagnostics;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class ResourceMonitor
{
    public virtual ResourceUsageDto GetCurrentUsage()
    {
        var process = Process.GetCurrentProcess();
        return new ResourceUsageDto(
            MemoryUsageMb: process.WorkingSet64 / (1024 * 1024),
            CpuUsagePercent: 0.0,  // Full CPU measurement requires two-sample approach; deferred
            DiskUsageMb: GetDiskUsageMb()
        );
    }

    private static long GetDiskUsageMb()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.CurrentDirectory);
            if (string.IsNullOrEmpty(root)) return 0;

            var drive = new DriveInfo(root);
            return (drive.TotalSize - drive.AvailableFreeSpace) / (1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }
}
