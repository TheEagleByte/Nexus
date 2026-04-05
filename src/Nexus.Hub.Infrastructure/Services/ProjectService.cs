using Microsoft.Extensions.Logging;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;

namespace Nexus.Hub.Infrastructure.Services;

public class ProjectService(IProjectRepository projectRepository, ISpokeRepository spokeRepository, ILogger<ProjectService> logger) : IProjectService
{
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly ISpokeRepository _spokeRepository = spokeRepository;
    private readonly ILogger<ProjectService> _logger = logger;

    public async Task<Project> CreateProjectAsync(Guid spokeId, string name, string? externalKey = null, string? summary = null, CancellationToken cancellationToken = default)
    {
        var spoke = await _spokeRepository.GetByIdAsync(spokeId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Spoke {spokeId} not found");

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            Name = name,
            ExternalKey = externalKey,
            Summary = summary,
            Status = ProjectStatus.Planning,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _projectRepository.AddAsync(project, cancellationToken);
        _logger.LogInformation("Project {ProjectId} created for spoke {SpokeId}: {ProjectName}", project.Id, spokeId, name);
        return project;
    }

    public async Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _logger.LogWarning("Project not found: {ProjectId}", projectId);
            throw new Domain.Exceptions.NotFoundException($"Project {projectId} not found");
        }
        return project;
    }

    public Task<List<Project>> ListProjectsAsync(Guid? spokeId = null, ProjectStatus? status = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        => _projectRepository.ListAsync(spokeId, status, limit, offset, cancellationToken);

    public async Task UpdateProjectStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException($"Project {projectId} not found");

        project.Status = status;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _projectRepository.UpdateAsync(project, cancellationToken);
        _logger.LogInformation("Project {ProjectId} status updated to {Status}", projectId, status);
    }

    public Task<int> GetProjectCountAsync(Guid? spokeId = null, ProjectStatus? status = null, CancellationToken cancellationToken = default)
        => _projectRepository.CountAsync(spokeId, status, cancellationToken);
}
