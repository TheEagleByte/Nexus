using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexus.Hub.Infrastructure.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<NexusDbContext>
{
    public NexusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NexusDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=nexus;Username=nexus;Password=nexus");
        return new NexusDbContext(optionsBuilder.Options);
    }
}
