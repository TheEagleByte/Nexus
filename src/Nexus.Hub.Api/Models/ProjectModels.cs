using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Models;

public class CreateProjectRequest
{
    public Guid SpokeId { get; set; }
    public string? ExternalKey { get; set; }
    public required string Name { get; set; }
    public string? Summary { get; set; }
    public string? ApprovalMode { get; set; }
}

public class UpdateProjectStatusRequest
{
    public ProjectStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class ProjectResponse
{
    public Guid Id { get; set; }
    public Guid SpokeId { get; set; }
    public string SpokeName { get; set; } = string.Empty;
    public string? ExternalKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int ActiveJobCount { get; set; }
    public int TotalJobCount { get; set; }
    public string? Summary { get; set; }
}

public class ProjectListResponse
{
    public List<ProjectResponse> Projects { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
