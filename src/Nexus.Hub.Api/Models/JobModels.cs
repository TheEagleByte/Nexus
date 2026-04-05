using System.Text.Json;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Api.Models;

public class CreateJobRequest
{
    public Guid ProjectId { get; set; }
    public JobType Type { get; set; }
    public bool RequiresApproval { get; set; }
    public JsonDocument? Context { get; set; }
}

public class ApproveJobRequest
{
    public bool Approved { get; set; }
    public JsonDocument? Modifications { get; set; }
}

public class CancelJobRequest
{
    public string? Reason { get; set; }
}

public class JobResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SpokeId { get; set; }
    public JobType Type { get; set; }
    public JobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Summary { get; set; }
}

public class JobDetailResponse : JobResponse
{
    public int OutputChunkCount { get; set; }
    public long OutputTotalBytes { get; set; }
    public JobProgress? Progress { get; set; }
    public JsonDocument? Metadata { get; set; }
}

public class JobProgress
{
    public int ElapsedSeconds { get; set; }
    public int? EstimatedTotalSeconds { get; set; }
}

public class JobListResponse
{
    public List<JobResponse> Jobs { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

public class JobOutputResponse
{
    public Guid JobId { get; set; }
    public List<OutputChunk> Chunks { get; set; } = [];
    public int TotalChunks { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool IsComplete { get; set; }
}

public class OutputChunk
{
    public long Sequence { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
