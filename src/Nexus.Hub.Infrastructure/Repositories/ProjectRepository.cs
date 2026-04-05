using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Infrastructure.Repositories;

public class ProjectRepository(NexusDbContext context) : IProjectRepository
{
    private readonly NexusDbContext _context = context;

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Projects.FindAsync([id], cancellationToken);

    public Task<List<Project>> ListBySpokeAsync(Guid spokeId, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<List<Project>> ListAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Project?> GetBySpokeAndExternalKeyAsync(Guid spokeId, string externalKey, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
