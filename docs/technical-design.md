# Nexus — Technical Design Document

**Status:** Implementation-Ready
**Last Updated:** 2026-04-04
**Stack:** .NET 10, Next.js 15+, PostgreSQL, SignalR, Self-Hosted Kubernetes

---

## 1. System Overview

Nexus is a self-hosted hub-and-spoke platform for orchestrating Claude Code workers across multiple machines. The **Hub** is a central command center (.NET 10 API + Next.js UI + PostgreSQL) accessible via a single dashboard. **Spokes** are persistent daemon agents running on individual machines (e.g., a primary workstation, secondary dev machine, local server), maintaining local workspace state and connecting outbound to the hub via SignalR WebSocket. **Workers** are ephemeral Docker containers spun up by spokes to execute individual coding tasks, powered by Claude Code CLI.

**Data Flow:**
- User submits work via hub UI → Hub persists job, broadcasts via SignalR → Spoke receives assignment → Spoke assembles context and launches worker container → Worker streams output back through spoke → Hub aggregates output for real-time display.
- All communication is initiated outbound from spoke to hub. No inbound firewall holes required on spoke machines.
- **No credentials or source code leave the spoke.** Only status, plans, summaries, and terminal output flow to the hub.

**Content Filtering:** The hub UI has basic display logic (don't show full code diffs in the UI), but Claude Code messages and CC output flow freely between spoke and hub. There is no content boundary filter or message filtering — all CC reasoning and tool output is relayed.

---

## 2. Hub Technical Design

### 2.1 API Layer (.NET 10)

#### Project Structure

```
Nexus.Hub/
├── Nexus.Hub.Api/                  # ASP.NET Core Web API
│   ├── Program.cs                  # DI, middleware setup
│   ├── Controllers/
│   │   ├── SpokeController.cs       # Spoke registration, status
│   │   ├── ProjectController.cs     # Project CRUD
│   │   ├── JobController.cs         # Job creation, approval, status
│   │   └── MessageController.cs     # Conversational message relay
│   ├── Hubs/
│   │   └── NexusHub.cs              # SignalR hub (spoke↔hub communication)
│   ├── Services/
│   │   ├── ISpokeService.cs         # Spoke management
│   │   ├── IJobService.cs           # Job orchestration
│   │   ├── IProjectService.cs       # Project management
│   │   └── IAuthenticationService.cs# Google OAuth + token management
│   ├── Models/                      # Request/response DTOs
│   ├── Middleware/
│   │   └── ExceptionMiddleware.cs   # Global error handling
│   └── appsettings.json
│
├── Nexus.Hub.Api.Tests/             # Unit tests for API layer (parallel to Api)
│   ├── Controllers/
│   ├── Services/
│   └── Hubs/
│
├── Nexus.Hub.Domain/                # Domain models, interfaces
│   ├── Entities/
│   │   ├── Spoke.cs
│   │   ├── Project.cs
│   │   ├── Job.cs
│   │   ├── Message.cs
│   │   └── OutputStream.cs
│   ├── Events/                      # Domain events
│   │   ├── JobCreatedEvent.cs
│   │   ├── JobStatusChangedEvent.cs
│   │   └── ProjectUpdatedEvent.cs
│   └── Repositories/               # Repository interfaces
│       ├── ISpokeRepository.cs
│       ├── IProjectRepository.cs
│       └── IJobRepository.cs
│
├── Nexus.Hub.Domain.Tests/          # Unit tests for domain logic
│
└── Nexus.Hub.Infrastructure/        # EF Core, concrete services
    ├── Data/
    │   ├── NexusDbContext.cs        # DbContext
    │   ├── Migrations/              # EF migrations
    │   └── DesignTimeFactory.cs     # For migrations
    ├── Repositories/                # Repository implementations
    ├── Services/                    # Service implementations
    ├── Authentication/              # Google OAuth handlers
    └── Tests/                       # Unit tests for infrastructure
```

**Note on Testing:** All components include unit tests as part of normal development. Tests are not separate stories; they live alongside the main projects in parallel test projects (e.g., `Nexus.Hub.Api.Tests`, `Nexus.Hub.Infrastructure.Tests`). Use xUnit + Moq for mocking.

**Architecture Decision:** Vertical slices + repositories. Each slice owns its domain (spoke, project, job), but repository abstractions provide flexibility for testing and future refactoring. Keep it simpler than full DDD but more maintainable than procedural controllers.

#### Core Service Signatures (C#)

```csharp
// Spoke Management
public interface ISpokeService
{
    Task<Spoke> RegisterSpokeAsync(SpokeRegistrationRequest request, string token);
    Task<Spoke> GetSpokeAsync(Guid spokeId);
    Task<List<Spoke>> ListSpokesAsync();
    Task UpdateSpokeStatusAsync(Guid spokeId, SpokeStatus status);
    Task UpdateSpokeHeartbeatAsync(Guid spokeId);
    Task<SpokeProfile> GetSpokeProfileAsync(Guid spokeId);
}

// Project Management
public interface IProjectService
{
    Task<Project> CreateProjectAsync(CreateProjectRequest request);
    Task<Project> GetProjectAsync(Guid projectId);
    Task<List<Project>> ListProjectsBySpokeAsync(Guid spokeId);
    Task UpdateProjectStatusAsync(Guid projectId, ProjectStatus status);
}

// Job Management
public interface IJobService
{
    Task<Job> CreateJobAsync(CreateJobRequest request);
    Task<Job> GetJobAsync(Guid jobId);
    Task<List<Job>> ListJobsByProjectAsync(Guid projectId);
    Task<List<Job>> ListPendingJobsBySpokeAsync(Guid spokeId);
    Task ApproveJobAsync(Guid jobId);
    Task CancelJobAsync(Guid jobId);
    Task RecordJobOutputAsync(Guid jobId, string output);
}

// Message Relay
public interface IMessageService
{
    Task<Message> RecordMessageAsync(Guid spokeId, MessageDirection direction, string content);
    Task<List<Message>> GetConversationAsync(Guid spokeId, int limit = 50);
}

// Pending Actions (Awaiting Input Queue)
public interface IPendingActionService
{
    Task<PendingAction> CreatePendingActionAsync(
        Guid spokeId, Guid projectId, PendingActionGateType gateType,
        string summary, string description, Dictionary<string, object> metadata);
    Task<PendingAction> GetPendingActionAsync(Guid id);
    Task<List<PendingAction>> ListPendingActionsAsync(
        Guid? spokeId = null, PendingActionGateType? gateType = null, bool unresolvedOnly = true);
    Task ResolvePendingActionAsync(Guid id, string action, string? notes);
}
```

#### Dependency Injection Setup (Program.cs)

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// Configure JSON serialization (enums as snake_case, ISO dates)
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    WriteIndented = false
};

builder.Services.Configure<JsonOptions>(o => o.SerializerOptions = jsonOptions);

// Database
builder.Services.AddDbContext<NexusDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.MigrationsAssembly("Nexus.Hub.Infrastructure")));

// Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "Bearer";
        options.DefaultChallengeScheme = "Bearer";
    })
    .AddScheme<BearerTokenAuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        "Bearer", options => { })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"];
        options.ClientSecret = builder.Configuration["Google:ClientSecret"];
    });

// Services
builder.Services.AddScoped<ISpokeService, SpokeService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ISpokeRepository, SpokeRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalOnly", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

var app = builder.Build();

app.UseRouting();
app.UseCors("LocalOnly");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NexusHub>("/api/hub");

app.Run();
```

#### REST Endpoints

**Spoke Management:**
```
POST   /api/spokes/register              # Register new spoke (token-based)
GET    /api/spokes                       # List all spokes
GET    /api/spokes/{spokeId}             # Get spoke details
PUT    /api/spokes/{spokeId}/status      # Update status
POST   /api/spokes/{spokeId}/heartbeat   # Heartbeat (spoke → hub)
```

**Projects:**
```
POST   /api/projects                     # Create project
GET    /api/projects/{projectId}         # Get project details
GET    /api/spokes/{spokeId}/projects    # List projects for spoke
PUT    /api/projects/{projectId}         # Update project status
```

**Jobs:**
```
POST   /api/jobs                         # Create job (hub → spoke, with idempotencyKey)
GET    /api/jobs/{jobId}                 # Get job details
GET    /api/projects/{projectId}/jobs    # List jobs for project
POST   /api/jobs/{jobId}/approve         # Approve job
POST   /api/jobs/{jobId}/cancel          # Cancel job
POST   /api/jobs/{jobId}/output          # Record output chunk (spoke → hub)
```

**Notes on Idempotency:**
- Job creation includes `idempotencyKey` field. Duplicate requests with same key return existing job.
- Spoke registration also uses idempotency to prevent duplicate registrations.
- Message sends should also be idempotent where possible.
```

**Messages:**
```
POST   /api/messages                     # Record/send message
GET    /api/spokes/{spokeId}/conversation  # Get conversation history
```

**Pending Actions (Awaiting Input Queue):**
```
GET    /api/pending-actions              # Get all awaiting items (cross-spoke)
POST   /api/pending-actions/{id}/resolve # Resolve action (approve/reject/respond)
```

**Authentication:**
```
POST   /api/auth/google                  # Google OAuth callback
GET    /api/auth/me                      # Current user info
POST   /api/auth/logout                  # Logout
```

---

### 2.2 SignalR Hub Design

The SignalR hub manages persistent WebSocket connections between hub and spokes. It handles bidirectional, real-time communication without polling.

#### NexusHub Class (C#)

```csharp
public class NexusHub : Hub
{
    private readonly ISpokeService _spokeService;
    private readonly IJobService _jobService;
    private readonly IMessageService _messageService;
    private readonly IHubContext<NexusHub> _hubContext;
    private readonly ILogger<NexusHub> _logger;

    // Map of connectionId → spokeId for tracking
    private static readonly Dictionary<string, Guid> ConnectionToSpokeMap = new();

    public NexusHub(
        ISpokeService spokeService,
        IJobService jobService,
        IMessageService messageService,
        IHubContext<NexusHub> hubContext,
        ILogger<NexusHub> logger)
    {
        _spokeService = spokeService;
        _jobService = jobService;
        _messageService = messageService;
        _hubContext = hubContext;
        _logger = logger;
    }

    // Spoke → Hub: Register connection
    public async Task SpokeRegisterAsync(SpokeRegistrationPayload payload)
    {
        var spokeId = payload.SpokeId;
        ConnectionToSpokeMap[Context.ConnectionId] = spokeId;

        await _spokeService.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Online);
        _logger.LogInformation($"Spoke {spokeId} registered with connection {Context.ConnectionId}");

        // Notify hub UI that spoke came online
        await Clients.All.SendAsync("spoke.connected", new { spokeId, timestamp = DateTime.UtcNow });

        // Send any pending jobs to the spoke
        var pendingJobs = await _jobService.ListPendingJobsBySpokeAsync(spokeId);
        if (pendingJobs.Any())
        {
            await Clients.Caller.SendAsync("jobs.pending", pendingJobs);
        }
    }

    // Spoke → Hub: Heartbeat
    public async Task SpokeHeartbeatAsync(SpokeHeartbeatPayload payload)
    {
        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
            return;

        await _spokeService.UpdateSpokeHeartbeatAsync(spokeId);
        _logger.LogDebug($"Heartbeat from spoke {spokeId}");
    }

    // Spoke → Hub: Project created/updated
    public async Task ProjectStatusChangedAsync(ProjectStatusPayload payload)
    {
        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
            return;

        await _jobService.UpdateProjectStatusAsync(payload.ProjectId, payload.Status);
        _logger.LogInformation($"Project {payload.ProjectId} status changed to {payload.Status}");

        // Broadcast to all clients
        await Clients.All.SendAsync("project.updated", payload);
    }

    // Spoke → Hub: Job status changed
    public async Task JobStatusChangedAsync(JobStatusPayload payload)
    {
        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
            return;

        await _jobService.UpdateJobStatusAsync(payload.JobId, payload.Status);
        _logger.LogInformation($"Job {payload.JobId} status changed to {payload.Status}");

        // Broadcast to all clients
        await Clients.All.SendAsync("job.updated", payload);
    }

    // Spoke → Hub: Terminal output chunk
    public async Task JobOutputAsync(JobOutputPayload payload)
    {
        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
            return;

        await _jobService.RecordJobOutputAsync(payload.JobId, payload.Output);

        // Stream to hub UI in real-time
        await Clients.All.SendAsync("job.output", new
        {
            jobId = payload.JobId,
            output = payload.Output,
            timestamp = DateTime.UtcNow
        });
    }

    // Spoke → Hub: Spoke sends message
    public async Task MessageFromSpokeAsync(MessagePayload payload)
    {
        if (!ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
            return;

        var message = await _messageService.RecordMessageAsync(
            spokeId, MessageDirection.SpokeToHub, payload.Content);

        // Relay to hub UI
        await Clients.All.SendAsync("message.received", message);
    }

    // Hub → Frontend: Pending action created (broadcasts to all UI clients)
    public async Task BroadcastPendingActionCreatedAsync(PendingActionEvent pendingActionEvent)
    {
        // Called from API when a pending action is created
        await Clients.All.SendAsync("pendingAction.created", pendingActionEvent);
        _logger.LogInformation($"Pending action {pendingActionEvent.Id} broadcast to UI clients");
    }

    // Hub → Frontend: Pending action resolved (broadcasts to all UI clients)
    public async Task BroadcastPendingActionResolvedAsync(PendingActionResolvedEvent resolvedEvent)
    {
        // Called from API when a pending action is resolved
        await Clients.All.SendAsync("pendingAction.resolved", resolvedEvent);
        _logger.LogInformation($"Pending action {resolvedEvent.Id} resolved and broadcast to UI clients");
    }

    // Hub → Spoke: Assign job (called from hub UI)
    public async Task AssignJobAsync(JobAssignmentPayload payload)
    {
        var spokeId = payload.SpokeId;
        var group = $"spoke-{spokeId}";

        // Send to specific spoke by group
        await Clients.Group(group).SendAsync("job.assign", payload);

        _logger.LogInformation($"Job assignment sent to spoke {spokeId}");
    }

    // Hub → Spoke: Send conversational message
    public async Task SendMessageToSpokeAsync(Guid spokeId, string message)
    {
        var group = $"spoke-{spokeId}";
        await Clients.Group(group).SendAsync("message.to_spoke", message);

        // Record for audit
        await _messageService.RecordMessageAsync(spokeId, MessageDirection.HubToSpoke, message);
    }

    // Hub → Spoke: Cancel job
    public async Task CancelJobAsync(Guid jobId, Guid spokeId)
    {
        var group = $"spoke-{spokeId}";
        await Clients.Group(group).SendAsync("job.cancel", jobId);

        _logger.LogInformation($"Job cancellation sent for {jobId}");
    }

    // Connection lifecycle
    public override async Task OnConnectedAsync()
    {
        var spokeId = Context.User?.FindFirst("spoke_id")?.Value;
        if (spokeId != null && Guid.TryParse(spokeId, out var spokeGuid))
        {
            var group = $"spoke-{spokeGuid}";
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToSpokeMap.TryGetValue(Context.ConnectionId, out var spokeId))
        {
            await _spokeService.UpdateSpokeStatusAsync(spokeId, SpokeStatus.Offline);
            ConnectionToSpokeMap.Remove(Context.ConnectionId);

            _logger.LogInformation($"Spoke {spokeId} disconnected");

            // Notify hub UI
            await Clients.All.SendAsync("spoke.disconnected", new { spokeId, timestamp = DateTime.UtcNow });
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

#### Connection Management

- **Groups:** Spokes are added to groups named `spoke-{spokeId}` on connection, allowing hub to broadcast to specific spokes.
- **Reconnection:** Spoke automatically reconnects on network loss (exponential backoff, max 5 min). Hub acknowledges reconnection and resends any queued jobs.
- **Backplane:** For single-instance hub (current), no backplane needed. If scaling to multiple hub instances, add Redis backplane: `services.AddSignalR().AddStackExchangeRedis(...)`.
- **Heartbeats:** Spoke sends heartbeat every 30 seconds. Hub times out spoke after 3 missed heartbeats (90 seconds).

---

### 2.3 Database Design

#### Entity Relationship Diagram (PostgreSQL)

```sql
-- Spokes
CREATE TABLE "Spokes" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Status" VARCHAR(50) NOT NULL,  -- Online, Offline, Busy
    "LastSeen" TIMESTAMP WITH TIME ZONE NOT NULL,
    "Capabilities" JSONB NOT NULL,  -- ["Jira", "Git", "Docker", ...]
    "Config" JSONB NOT NULL,         -- Approval modes, concurrency limits
    "Profile" JSONB,                 -- SpokeProfile: display name, description, repos, Jira instances, integrations
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_spokes_status ON "Spokes"("Status");
CREATE INDEX idx_spokes_last_seen ON "Spokes"("LastSeen");

-- Projects
CREATE TABLE "Projects" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "ExternalKey" VARCHAR(255),      -- Jira ticket key, e.g., "PROJ-4521"
    "Name" VARCHAR(255) NOT NULL,
    "Summary" TEXT,
    "Status" VARCHAR(50) NOT NULL,   -- Planning, Active, Paused, Completed, Failed
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_projects_spoke_id ON "Projects"("SpokeId");
CREATE INDEX idx_projects_external_key ON "Projects"("ExternalKey");
CREATE INDEX idx_projects_status ON "Projects"("Status");
CREATE UNIQUE INDEX idx_projects_spoke_external_key ON "Projects"("SpokeId", "ExternalKey");

-- Jobs
CREATE TABLE "Jobs" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "ProjectId" UUID NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "Status" VARCHAR(50) NOT NULL,   -- Queued, AwaitingApproval, Running, Completed, Failed, Cancelled
    "Type" VARCHAR(50) NOT NULL,     -- Implement, Test, Refactor, Investigate, Custom
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "StartedAt" TIMESTAMP WITH TIME ZONE,
    "CompletedAt" TIMESTAMP WITH TIME ZONE,
    "Summary" TEXT,                  -- Agent-generated outcome summary
    "ApprovalRequired" BOOLEAN NOT NULL DEFAULT FALSE,
    "ApprovedAt" TIMESTAMP WITH TIME ZONE,
    "ApprovedBy" VARCHAR(255)        -- User ID (for audit)
);
CREATE INDEX idx_jobs_project_id ON "Jobs"("ProjectId");
CREATE INDEX idx_jobs_spoke_id ON "Jobs"("SpokeId");
CREATE INDEX idx_jobs_status ON "Jobs"("Status");
CREATE INDEX idx_jobs_created_at ON "Jobs"("CreatedAt" DESC);

-- Messages
CREATE TABLE "Messages" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "Direction" VARCHAR(50) NOT NULL,  -- UserToSpoke, SpokeToUser, System
    "Content" TEXT NOT NULL,
    "JobId" UUID REFERENCES "Jobs"("Id") ON DELETE SET NULL,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_messages_spoke_id ON "Messages"("SpokeId");
CREATE INDEX idx_messages_job_id ON "Messages"("JobId");
CREATE INDEX idx_messages_timestamp ON "Messages"("Timestamp" DESC);

-- Output Stream
CREATE TABLE "OutputStreams" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "JobId" UUID NOT NULL REFERENCES "Jobs"("Id") ON DELETE CASCADE,
    "Sequence" BIGINT NOT NULL,
    "Content" TEXT NOT NULL,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_output_streams_job_id ON "OutputStreams"("JobId", "Sequence");
CREATE UNIQUE INDEX idx_output_streams_job_sequence ON "OutputStreams"("JobId", "Sequence");

-- Pending Actions (Awaiting Input Queue)
CREATE TABLE "PendingActions" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "ProjectId" UUID NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
    "GateType" VARCHAR(50) NOT NULL,  -- PlanReview, PreExecution, PostExecution, SpokeQuestion, PrReview
    "Summary" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Metadata" JSONB NOT NULL,        -- Gate-type-specific data (plan content, branch name, etc.)
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "ResolvedAt" TIMESTAMP WITH TIME ZONE,
    "ResolvedBy" VARCHAR(255)         -- User ID (for audit)
);
CREATE INDEX idx_pending_actions_spoke_id ON "PendingActions"("SpokeId");
CREATE INDEX idx_pending_actions_project_id ON "PendingActions"("ProjectId");
CREATE INDEX idx_pending_actions_gate_type ON "PendingActions"("GateType");
CREATE INDEX idx_pending_actions_created_at ON "PendingActions"("CreatedAt" DESC);
CREATE INDEX idx_pending_actions_resolved_at ON "PendingActions"("ResolvedAt");
-- Find all unresolved (awaiting input)
CREATE INDEX idx_pending_actions_unresolved ON "PendingActions"("ResolvedAt") WHERE "ResolvedAt" IS NULL;

-- Users (for authentication and audit)
CREATE TABLE "Users" (
    "Id" VARCHAR(255) PRIMARY KEY NOT NULL,  -- Google sub
    "Email" VARCHAR(255) NOT NULL UNIQUE,
    "Name" VARCHAR(255),
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);

-- Pending Commands for Offline Spokes (queued for reconnection)
CREATE TABLE "PendingCommands" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "CommandType" VARCHAR(50) NOT NULL,  -- "job_assign", "message", "config_update"
    "Payload" JSONB NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "DeliveredAt" TIMESTAMP WITH TIME ZONE,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL  -- 24-hour TTL
);
CREATE INDEX idx_pending_commands_spoke_id ON "PendingCommands"("SpokeId");
CREATE INDEX idx_pending_commands_delivered_at ON "PendingCommands"("DeliveredAt") WHERE "DeliveredAt" IS NULL;

-- PR Comment Processing Tracking (for deduplication)
CREATE TABLE "ProcessedPrComments" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL REFERENCES "Spokes"("Id") ON DELETE CASCADE,
    "RepositoryPath" VARCHAR(255) NOT NULL,
    "PrNumber" VARCHAR(50) NOT NULL,
    "CommentId" VARCHAR(255) NOT NULL,
    "ParentCommentId" VARCHAR(255),      -- For threading: comment this is a reply to
    "ProcessedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "ActionTaken" VARCHAR(50) NOT NULL,  -- "created_job", "responded", "escalated", "marked_positive"
    "ResponseJobId" UUID,                -- Job ID created for this comment (if actionable)
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE UNIQUE INDEX idx_processed_pr_comments_unique ON "ProcessedPrComments"("SpokeId", "RepositoryPath", "CommentId");
CREATE INDEX idx_processed_pr_comments_spoke_id ON "ProcessedPrComments"("SpokeId");
CREATE INDEX idx_processed_pr_comments_pr_number ON "ProcessedPrComments"("RepositoryPath", "PrNumber");

-- Audit Log (all SignalR events and API calls)
CREATE TABLE "AuditLogs" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "UserId" VARCHAR(255) REFERENCES "Users"("Id"),
    "SpokeId" UUID REFERENCES "Spokes"("Id"),
    "Action" VARCHAR(255) NOT NULL,          -- "SpokeRegistered", "JobCreated", etc.
    "Details" JSONB,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_audit_logs_timestamp ON "AuditLogs"("Timestamp" DESC);
```

#### EF Core DbContext

```csharp
public class NexusDbContext : DbContext
{
    public DbSet<Spoke> Spokes { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<OutputStream> OutputStreams { get; set; }
    public DbSet<PendingAction> PendingActions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    public NexusDbContext(DbContextOptions<NexusDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Spokes
        modelBuilder.Entity<Spoke>()
            .HasKey(s => s.Id);
        modelBuilder.Entity<Spoke>()
            .HasMany(s => s.Projects)
            .WithOne(p => p.Spoke)
            .HasForeignKey(p => p.SpokeId);

        // Projects
        modelBuilder.Entity<Project>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<Project>()
            .HasIndex(p => new { p.SpokeId, p.ExternalKey })
            .IsUnique();

        // Jobs
        modelBuilder.Entity<Job>()
            .HasKey(j => j.Id);
        modelBuilder.Entity<Job>()
            .HasMany(j => j.OutputStreams)
            .WithOne(o => o.Job)
            .HasForeignKey(o => o.JobId);

        // Messages
        modelBuilder.Entity<Message>()
            .HasKey(m => m.Id);

        // OutputStream
        modelBuilder.Entity<OutputStream>()
            .HasKey(o => new { o.JobId, o.Sequence });

        // PendingActions
        modelBuilder.Entity<PendingAction>()
            .HasKey(pa => pa.Id);
        modelBuilder.Entity<PendingAction>()
            .HasIndex(pa => new { pa.SpokeId })
            .HasName("idx_pending_actions_spoke_id");
        modelBuilder.Entity<PendingAction>()
            .HasIndex(pa => new { pa.ProjectId })
            .HasName("idx_pending_actions_project_id");
        modelBuilder.Entity<PendingAction>()
            .HasIndex(pa => pa.GateType)
            .HasName("idx_pending_actions_gate_type");
        modelBuilder.Entity<PendingAction>()
            .HasIndex(pa => pa.CreatedAt)
            .HasName("idx_pending_actions_created_at");
    }
}
```

#### Key Queries

```csharp
// Get active spokes
var activeSpokes = await dbContext.Spokes
    .Where(s => s.Status == SpokeStatus.Online)
    .OrderBy(s => s.Name)
    .ToListAsync();

// Get jobs by project with output
var jobsWithOutput = await dbContext.Jobs
    .Include(j => j.OutputStreams)
    .Where(j => j.ProjectId == projectId)
    .OrderByDescending(j => j.CreatedAt)
    .Take(50)
    .ToListAsync();

// Get recent messages for spoke
var messages = await dbContext.Messages
    .Where(m => m.SpokeId == spokeId)
    .OrderByDescending(m => m.Timestamp)
    .Take(100)
    .ToListAsync();

// Get pending jobs for spoke (for reconnection resync)
var pending = await dbContext.Jobs
    .Where(j => j.SpokeId == spokeId && j.Status == JobStatus.Queued)
    .ToListAsync();
```

#### Migration Strategy

- Use EF Core migrations for schema versioning.
- Migrations live in `Nexus.Hub.Infrastructure/Migrations/`.
- Run migrations on hub startup (idempotent): `context.Database.Migrate();` in Program.cs.
- For local dev, use `dotnet ef migrations add <name>` and `dotnet ef database update`.

---

### 2.4 Frontend (Next.js 15)

#### Project Structure

```
nexus-web/
├── app/
│   ├── layout.tsx                    # Root layout
│   ├── page.tsx                      # Redirect to /dashboard
│   ├── auth/
│   │   ├── layout.tsx
│   │   ├── signin/page.tsx           # Google OAuth signin
│   │   └── callback/page.tsx         # OAuth redirect handler
│   ├── dashboard/
│   │   ├── layout.tsx
│   │   ├── page.tsx                  # Spoke list + status overview
│   │   ├── spokes/
│   │   │   ├── [spokeId]/page.tsx    # Spoke detail + conversation
│   │   │   └── [spokeId]/projects/page.tsx
│   │   ├── projects/
│   │   │   ├── page.tsx              # All projects across spokes
│   │   │   └── [projectId]/page.tsx  # Project detail + job history
│   │   ├── jobs/
│   │   │   ├── page.tsx              # Recent jobs feed
│   │   │   └── [jobId]/page.tsx      # Job detail + live output
│   │   └── timeline/page.tsx         # Cross-spoke activity timeline
│   └── api/
│       └── auth/
│           ├── signin/route.ts       # OAuth flow
│           └── callback/route.ts
├── components/
│   ├── ui/                           # shadcn/ui components
│   │   ├── card.tsx
│   │   ├── button.tsx
│   │   ├── input.tsx
│   │   ├── textarea.tsx
│   │   ├── badge.tsx
│   │   ├── select.tsx
│   │   ├── dialog.tsx
│   │   └── scroll-area.tsx
│   ├── layout/
│   │   ├── Header.tsx
│   │   ├── Sidebar.tsx
│   │   └── MainLayout.tsx
│   ├── spokes/
│   │   ├── SpokeCard.tsx
│   │   ├── SpokeList.tsx
│   │   └── SpokeConversation.tsx
│   ├── projects/
│   │   ├── ProjectCard.tsx
│   │   └── ProjectDetail.tsx
│   ├── jobs/
│   │   ├── JobCard.tsx
│   │   ├── JobDetail.tsx
│   │   ├── JobOutputViewer.tsx
│   │   └── CreateJobDialog.tsx
│   └── common/
│       ├── StatusBadge.tsx
│       ├── LoadingSpinner.tsx
│       └── ErrorBoundary.tsx
├── lib/
│   ├── api.ts                        # HTTP client + endpoints
│   ├── signalr.ts                    # SignalR connection manager
│   ├── auth.ts                       # Auth helpers
│   ├── hooks.ts                      # Custom hooks (useSpoke, useJob, etc.)
│   └── types.ts                      # TypeScript types (mirrors .NET models)
├── styles/
│   └── globals.css                   # Tailwind + custom styles
├── middleware.ts                     # NextAuth middleware
├── next.config.js
├── tsconfig.json
└── package.json
```

#### TypeScript Types (lib/types.ts)

```typescript
// Entities (enums serialize as snake_case on wire)
export interface Spoke {
  id: string;
  name: string;
  status: "online" | "offline" | "busy";  // snake_case on wire
  lastSeen: string;
  capabilities: string[];
  config: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

export interface Project {
  id: string;
  spokeId: string;
  externalKey?: string;
  name: string;
  summary?: string;
  status: "planning" | "active" | "paused" | "completed" | "failed";  // snake_case on wire
  createdAt: string;
  updatedAt: string;
}

export interface Job {
  id: string;
  projectId: string;
  spokeId: string;
  status: "queued" | "awaiting_approval" | "running" | "completed" | "failed" | "cancelled";  // snake_case on wire
  type: "implement" | "test" | "refactor" | "investigate" | "custom";  // snake_case on wire
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  summary?: string;
  approvalRequired: boolean;
  approvedAt?: string;
  approvedBy?: string;
}

export interface Message {
  id: string;
  spokeId: string;
  direction: "user_to_spoke" | "spoke_to_user" | "system";  // snake_case on wire
  content: string;
  jobId?: string;
  timestamp: string;
}

export interface OutputStream {
  jobId: string;
  sequence: number;
  content: string;
  timestamp: string;
}

// API Requests
export interface CreateJobRequest {
  projectId: string;
  type: Job["type"];
  approvalRequired: boolean;
  metadata?: Record<string, any>;
}

export interface ApproveJobRequest {
  jobId: string;
  modifications?: Record<string, any>;
}

// SignalR Events
export interface SignalRPayload {
  eventType: string;
  timestamp: string;
  correlationId: string;
  payload: any;
}
```

#### API Client (lib/api.ts)

```typescript
import { useSession } from "next-auth/react";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api";

export async function fetchAPI<T>(
  endpoint: string,
  options?: RequestInit
): Promise<T> {
  const { data: session } = useSession();

  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session?.accessToken}`,
      ...options?.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.status}`);
  }

  return response.json();
}

// Spoke endpoints
export const spokeApi = {
  list: () => fetchAPI("/spokes"),
  get: (spokeId: string) => fetchAPI(`/spokes/${spokeId}`),
  updateStatus: (spokeId: string, status: string) =>
    fetchAPI(`/spokes/${spokeId}/status`, { method: "PUT", body: JSON.stringify({ status }) }),
};

// Project endpoints
export const projectApi = {
  create: (spokeId: string, data: any) =>
    fetchAPI("/projects", { method: "POST", body: JSON.stringify({ ...data, spokeId }) }),
  get: (projectId: string) => fetchAPI(`/projects/${projectId}`),
  listBySpo: (spokeId: string) => fetchAPI(`/spokes/${spokeId}/projects`),
};

// Job endpoints
export const jobApi = {
  create: (data: CreateJobRequest) =>
    fetchAPI("/jobs", { method: "POST", body: JSON.stringify(data) }),
  get: (jobId: string) => fetchAPI(`/jobs/${jobId}`),
  listByProject: (projectId: string) => fetchAPI(`/projects/${projectId}/jobs`),
  approve: (jobId: string) =>
    fetchAPI(`/jobs/${jobId}/approve`, { method: "POST" }),
  cancel: (jobId: string) =>
    fetchAPI(`/jobs/${jobId}/cancel`, { method: "POST" }),
};

// Message endpoints
export const messageApi = {
  send: (spokeId: string, content: string) =>
    fetchAPI("/messages", {
      method: "POST",
      body: JSON.stringify({ spokeId, content, direction: "UserToSpoke" }),
    }),
  history: (spokeId: string) => fetchAPI(`/spokes/${spokeId}/conversation`),
};
```

#### SignalR Connection Manager (lib/signalr.ts)

```typescript
import * as signalR from "@microsoft/signalr";
import { useCallback, useEffect, useRef } from "react";

class NexusSignalRClient {
  private connection: signalR.HubConnection | null = null;
  private callbacks: Map<string, Function[]> = new Map();

  async connect(token: string): Promise<void> {
    const url = `${process.env.NEXT_PUBLIC_API_URL}/api/hub`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => Promise.resolve(token),
        withCredentials: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .withHubProtocol(new signalR.JsonHubProtocol())
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register handlers for all event types
    this.connection.on("spoke.connected", (data) => this.emit("spoke.connected", data));
    this.connection.on("spoke.disconnected", (data) => this.emit("spoke.disconnected", data));
    this.connection.on("job.updated", (data) => this.emit("job.updated", data));
    this.connection.on("job.output", (data) => this.emit("job.output", data));
    this.connection.on("project.updated", (data) => this.emit("project.updated", data));
    this.connection.on("message.received", (data) => this.emit("message.received", data));

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      await this.connection.stop();
    }
  }

  subscribe(eventType: string, callback: Function): () => void {
    if (!this.callbacks.has(eventType)) {
      this.callbacks.set(eventType, []);
    }
    this.callbacks.get(eventType)!.push(callback);

    // Return unsubscribe function
    return () => {
      const callbacks = this.callbacks.get(eventType);
      if (callbacks) {
        const index = callbacks.indexOf(callback);
        if (index > -1) callbacks.splice(index, 1);
      }
    };
  }

  private emit(eventType: string, data: any): void {
    const callbacks = this.callbacks.get(eventType) || [];
    callbacks.forEach((cb) => cb(data));
  }

  async sendMessage(method: string, ...args: any[]): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke(method, ...args);
    }
  }
}

export const signalRClient = new NexusSignalRClient();

// Hook for using SignalR in components
export function useSignalR(eventType: string, callback: Function) {
  useEffect(() => {
    return signalRClient.subscribe(eventType, callback);
  }, [eventType, callback]);
}
```

#### Custom Hooks (lib/hooks.ts)

```typescript
import { useEffect, useState } from "react";
import { useSession } from "next-auth/react";
import { signalRClient, useSignalR } from "./signalr";
import * as api from "./api";

export function useSpokes() {
  const [spokes, setSpokes] = useState<Spoke[]>([]);
  const [loading, setLoading] = useState(true);
  const { data: session } = useSession();

  useEffect(() => {
    if (!session) return;

    const loadSpokes = async () => {
      try {
        const data = await api.spokeApi.list();
        setSpokes(data);
      } catch (error) {
        console.error("Failed to load spokes:", error);
      } finally {
        setLoading(false);
      }
    };

    loadSpokes();
  }, [session]);

  useSignalR("spoke.connected", (data) => {
    setSpokes((prev) =>
      prev.map((s) => (s.id === data.spokeId ? { ...s, status: "Online" } : s))
    );
  });

  useSignalR("spoke.disconnected", (data) => {
    setSpokes((prev) =>
      prev.map((s) => (s.id === data.spokeId ? { ...s, status: "Offline" } : s))
    );
  });

  return { spokes, loading };
}

export function useJobs(projectId?: string) {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(!!projectId);

  useEffect(() => {
    if (!projectId) return;

    const loadJobs = async () => {
      try {
        const data = await api.jobApi.listByProject(projectId);
        setJobs(data);
      } catch (error) {
        console.error("Failed to load jobs:", error);
      } finally {
        setLoading(false);
      }
    };

    loadJobs();
  }, [projectId]);

  useSignalR("job.updated", (data) => {
    setJobs((prev) =>
      prev.map((j) => (j.id === data.jobId ? { ...j, status: data.status } : j))
    );
  });

  return { jobs, loading };
}

export function useJobOutput(jobId: string) {
  const [output, setOutput] = useState<string>("");

  useSignalR("job.output", (data) => {
    if (data.jobId === jobId) {
      setOutput((prev) => prev + data.output);
    }
  });

  return { output };
}
```

#### Key Pages

**Dashboard (/dashboard/page.tsx):**
```typescript
export default function Dashboard() {
  const { spokes, loading } = useSpokes();

  if (loading) return <LoadingSpinner />;

  return (
    <MainLayout>
      <div className="space-y-6">
        <h1 className="text-3xl font-bold">Connected Spokes</h1>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {spokes.map((spoke) => (
            <SpokeCard key={spoke.id} spoke={spoke} />
          ))}
        </div>
        <ActivityTimeline />
      </div>
    </MainLayout>
  );
}
```

**Job Detail (/dashboard/jobs/[jobId]/page.tsx):**
```typescript
export default function JobDetail({ params }: { params: { jobId: string } }) {
  const [job, setJob] = useState<Job | null>(null);
  const { output } = useJobOutput(params.jobId);

  useEffect(() => {
    const loadJob = async () => {
      const data = await api.jobApi.get(params.jobId);
      setJob(data);
    };
    loadJob();
  }, [params.jobId]);

  if (!job) return <LoadingSpinner />;

  return (
    <MainLayout>
      <Card>
        <h1 className="text-2xl font-bold">{job.id}</h1>
        <StatusBadge status={job.status} />
        <JobOutputViewer output={output} />
      </Card>
    </MainLayout>
  );
}
```

#### Authentication (NextAuth.js)

```typescript
// middleware.ts
import { withAuth } from "next-auth/middleware";

export const middleware = withAuth({
  callbacks: {
    authorized: ({ token }) => !!token,
  },
});

export const config = {
  matcher: ["/dashboard/:path*", "/api/protected/:path*"],
};

// [...nextauth].ts - NextAuth JWT with refresh tokens
import NextAuth from "next-auth";
import GoogleProvider from "next-auth/providers/google";

export const authOptions = {
  providers: [
    GoogleProvider({
      clientId: process.env.GOOGLE_CLIENT_ID!,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET!,
    }),
  ],
  jwt: {
    maxAge: 15 * 60,  // 15 minutes
  },
  session: {
    maxAge: 7 * 24 * 60 * 60,  // 7 days for refresh token
  },
  callbacks: {
    async jwt({ token, account }) {
      if (account) {
        token.accessToken = account.access_token;
        token.accessTokenExpires = Date.now() + 15 * 60 * 1000;  // 15 minutes from now
      }

      // Auto-refresh if expired
      if (Date.now() >= (token.accessTokenExpires ?? 0)) {
        return refreshAccessToken(token);
      }

      return token;
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken;
      session.accessTokenExpires = token.accessTokenExpires;
      return session;
    },
  },
};

async function refreshAccessToken(token: any) {
  // Implement refresh token call to GET /api/auth/refresh
  // Spoke holds refresh token in httpOnly cookie
  return token;
}

export default NextAuth(authOptions);
```

#### State Management

Keep state management simple for now:
- **Component state** for local UI state (form inputs, modals, etc.).
- **Custom hooks** with SignalR subscriptions for real-time data (spokes, jobs, messages).
- **URL params** for routing and deep-linking (e.g., `/dashboard/jobs/[jobId]`).
- **Session** from NextAuth for auth state.

No Redux, Zustand, or other global store needed at this phase. Move to Zustand if state complexity grows.

#### Backend for Frontend (BFF) Note

The Next.js frontend acts as its own BFF (Backend for Frontend). The BFF and frontend live at the same host (e.g., `localhost:3000` for dev). The BFF calls the .NET API at `localhost:5000`. No CORS issues between frontend and BFF — CORS headers only matter if external clients call the .NET API directly. For dev simplicity, allow localhost:3000 CORS origin on the .NET API in case the frontend needs direct access.

---

## 2.4 Hub CC Meta-Agent

The hub runs a persistent Claude Code instance (the "meta-agent") that answers cross-system questions. This is NOT part of the .NET API; it's a separate process managed by the hub service.

### Invocation Pattern

```csharp
// HubMetaAgentService.cs

public class HubMetaAgentService
{
    private readonly string _ccSessionId;
    private readonly string _skillsPath;
    private readonly ILogger<HubMetaAgentService> _logger;

    public HubMetaAgentService(IConfiguration config, ILogger<HubMetaAgentService> logger)
    {
        _ccSessionId = config["Hub:CCSessionId"] ?? Guid.NewGuid().ToString();
        _skillsPath = config["Hub:SkillsPath"] ?? "/app/skills";
        _logger = logger;
    }

    public async Task<string> AskAsync(string message, string context = "")
    {
        // Invoke CC with --resume and current session
        var args = new List<string>
        {
            "--resume",
            _ccSessionId,
            "--skills-path", _skillsPath
        };

        if (!string.IsNullOrEmpty(context))
        {
            args.AddRange(new[] { "--context", context });
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cc",
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Send message via stdin
        await process.StandardInput.WriteLineAsync(message);
        process.StandardInput.Close();

        // Capture output
        var output = await process.StandardOutput.ReadToEndAsync();
        var errors = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError($"CC invocation failed: {errors}");
            throw new Exception($"Hub meta-agent failed: {errors}");
        }

        return output;
    }
}
```

### Hub Skills

Hub-level skills live at `/app/skills/` and teach the meta-agent how to:
- Query spokes via hub-local tools (proxied over SignalR)
- Aggregate status across all spokes
- Identify bottlenecks and high-priority items
- Reason about cross-project dependencies
- Generate proactive insights

Example skill: `synthesis.md` teaches the meta-agent how to interpret query results and synthesize actionable summaries.

### Hub-Local Tools Availability in Hub Skills

Hub skills can reference hub-local tools. When the meta-agent needs to query a spoke, it invokes a hub-local tool (e.g., `query_spoke(spoke_id, question)`) which sends a SignalR message to the spoke and returns the response. The hub NEVER connects to spoke MCPs directly.

---

## 2.5 Conversation Management

Conversations are persistent threads stored in PostgreSQL and mirrored to the hub from spokes.

### Conversation Entities

```sql
CREATE TABLE "Conversations" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID REFERENCES "Spokes"("Id") ON DELETE CASCADE,  -- NULL for hub-level
    "Title" VARCHAR(255) NOT NULL,
    "CCSessionId" VARCHAR(255) NOT NULL,  -- Session ID on spoke or hub
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_conversations_spoke_id ON "Conversations"("SpokeId");
CREATE INDEX idx_conversations_session_id ON "Conversations"("CCSessionId");

CREATE TABLE "ConversationMessages" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "ConversationId" UUID NOT NULL REFERENCES "Conversations"("Id") ON DELETE CASCADE,
    "Role" VARCHAR(50) NOT NULL,  -- user, assistant, system
    "Content" TEXT NOT NULL,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE INDEX idx_conversation_messages_conversation_id ON "ConversationMessages"("ConversationId");
CREATE INDEX idx_conversation_messages_timestamp ON "ConversationMessages"("Timestamp" DESC);
```

### Conversation Sync Flow

1. **User sends message to spoke** via hub UI → Hub API creates ConversationMessage (role: user) and sends via SignalR to spoke.
2. **Spoke receives message** → Spoke invokes CC with `--resume` and CC session ID → CC processes message with full context → Spoke captures output.
3. **Spoke sends response back** → Spoke sends ConversationMessage (role: assistant) and output back to hub via SignalR.
4. **Hub stores message** → Hub creates ConversationMessage record and broadcasts to UI clients.
5. **User starts new conversation** → Hub UI creates new Conversation record with new CC session ID. Spoke starts fresh CC session with that ID.

### Session Lifecycle

- **Hub-level conversation:** Hub creates Conversation with `spoke_id = NULL`, generates CC session ID, starts new CC instance with that session.
- **Spoke conversation:** Hub creates Conversation with the spoke's ID, provides CC session ID to spoke. Spoke resumes that session for all messages in this thread.
- **Switching conversations:** Hub UI loads list of Conversations for the spoke from DB. User picks one, hub resumes that CC session on the spoke.

---

## 3. Hub-Local Tools & Spoke Query Pattern

### 3.1 Hub-Local Tools (NOT MCP Servers)

The hub CC meta-agent uses hub-local tools to query spokes. These tools are NOT MCP servers on the spoke. Instead, they send SignalR messages over the existing WebSocket connection.

#### Hub-Local Tool Definitions (Two-Tier Query System)

The hub exposes two distinct spoke query tools with different latency profiles:

- **query_spoke_status(spoke_id)** — Fast: returns cached spoke state immediately (no CC invocation). Sub-second latency.
- **query_spoke(spoke_id, question)** — Slow: sends SignalR message to spoke → spoke spins up CC container → CC reasons about query → response returned. 5-15 second latency.

```yaml
# Hub CC MCP config (hub-mcp-config.json)
# These are hub-local tools, not spoke MCPs
{
  "tools": [
    {
      "name": "query_spoke_status",
      "description": "Get cached status of a specific spoke (fast, no reasoning). Returns machine info, online status, active jobs.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "spoke_id": {"type": "string", "description": "Spoke ID to query"}
        },
        "required": ["spoke_id"]
      }
    },
    {
      "name": "query_spoke",
      "description": "Send a query to a specific spoke for reasoning (slow, invokes CC on spoke). Use for complex questions; use query_spoke_status for fast lookups.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "spoke_id": {"type": "string", "description": "Spoke ID to query"},
          "question": {"type": "string", "description": "Question or query for CC to reason about"}
        },
        "required": ["spoke_id", "question"]
      }
    },
    {
      "name": "list_spokes",
      "description": "List all connected spokes with status",
      "inputSchema": {"type": "object", "properties": {}}
    },
    {
      "name": "get_cross_spoke_summary",
      "description": "Aggregated status across all spokes",
      "inputSchema": {"type": "object", "properties": {}}
    },
    {
      "name": "list_all_pending_actions",
      "description": "All HITL items across all spokes",
      "inputSchema": {
        "type": "object",
        "properties": {
          "unresolvedOnly": {"type": "boolean"}
        }
      }
    },
    {
      "name": "search_projects",
      "description": "Search projects across all spokes by name or key",
      "inputSchema": {
        "type": "object",
        "properties": {
          "query": {"type": "string"}
        },
        "required": ["query"]
      }
    },
    {
      "name": "get_timeline",
      "description": "Recent activity across system",
      "inputSchema": {
        "type": "object",
        "properties": {
          "limit": {"type": "integer", "description": "Number of recent events to retrieve"}
        }
      }
    }
  ]
}
```

#### Implementation (Hub)

```csharp
// HubLocalTools.cs - Implements hub-local tools

public class HubLocalTools
{
    private readonly NexusHub _hub;
    private readonly ISpokeService _spokeService;
    private readonly IProjectService _projectService;
    private readonly ILogger<HubLocalTools> _logger;

    public HubLocalTools(...) { ... }

    // Hub-local tool: query_spoke sends a SignalR message to spoke and waits for response
    public async Task<object> QuerySpokeAsync(string spokeId, string question)
    {
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();

        // Register a handler for the response
        _hub.RegisterQueryResponseHandler(correlationId, response =>
        {
            tcs.SetResult(response);
        });

        try
        {
            // Send the query over SignalR
            await _hub.InvokeAsync("ReceiveSpokeQuery", new
            {
                CorrelationId = correlationId,
                Query = question,
                Context = ""
            }, spokeId);

            // Wait for response with timeout
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Query to spoke {spokeId} timed out");
            throw;
        }
    }

    // Hub-local tools that query hub database
    public async Task<object> ListSpokesAsync()
    {
        return await _spokeService.ListAllAsync();
    }

    public async Task<object> GetCrossSpokeSummaryAsync()
    {
        var spokes = await _spokeService.ListAllAsync();
        return new
        {
            totalSpokes = spokes.Count,
            onlineSpokes = spokes.Count(s => s.Status == SpokeStatus.Online),
            activeJobs = spokes.Sum(s => s.ActiveJobCount),
            pendingActions = await GetAllPendingActionsCountAsync()
        };
    }

    public async Task<object> ListAllPendingActionsAsync(bool unresolvedOnly = true)
    {
        return await _spokeService.ListAllPendingActionsAsync(unresolvedOnly);
    }

    public async Task<object> SearchProjectsAsync(string query)
    {
        return await _projectService.SearchAsync(query);
    }

    public async Task<object> GetTimelineAsync(int limit = 20)
    {
        return await _spokeService.GetTimelineEventsAsync(limit);
    }
}
```

### 3.1 Pending Commands for Offline Spokes

When a spoke goes offline, the hub queues commands (job assignments, config updates, messages) in the `pending_commands` table. When the spoke reconnects, the hub delivers these commands in order. Each command has a 24-hour TTL; if the spoke doesn't reconnect within 24 hours, the user is notified and the command is archived.

**Implementation:**

```csharp
public class PendingCommandService
{
    private readonly NexusDbContext _db;
    private readonly IHubContext<NexusHub> _hubContext;

    public async Task<PendingCommand> QueueCommandAsync(
        Guid spokeId,
        string commandType,
        object payload)
    {
        var command = new PendingCommand
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            CommandType = commandType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _db.PendingCommands.Add(command);
        await _db.SaveChangesAsync();
        return command;
    }

    public async Task<List<PendingCommand>> GetUndeliveredCommandsAsync(Guid spokeId)
    {
        return await _db.PendingCommands
            .Where(pc => pc.SpokeId == spokeId && pc.DeliveredAt == null)
            .OrderBy(pc => pc.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkDeliveredAsync(Guid commandId)
    {
        var command = await _db.PendingCommands.FindAsync(commandId);
        if (command != null)
        {
            command.DeliveredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // Background task: clean up expired commands, notify user
    public async Task CleanupExpiredCommandsAsync()
    {
        var expired = await _db.PendingCommands
            .Where(pc => pc.ExpiresAt < DateTime.UtcNow && pc.DeliveredAt == null)
            .ToListAsync();

        foreach (var cmd in expired)
        {
            // Notify user: "Command XYZ for spoke Y expired"
            await _hubContext.Clients.All.SendAsync("command.expired",
                new { commandId = cmd.Id, spokeId = cmd.SpokeId });
            _db.PendingCommands.Remove(cmd);
        }

        await _db.SaveChangesAsync();
    }
}
```

**On Spoke Reconnection:**

```csharp
// In NexusHub.SpokeRegisterAsync
var pendingCommands = await _pendingCommandService.GetUndeliveredCommandsAsync(spokeId);
foreach (var cmd in pendingCommands)
{
    // Send command to spoke
    await Clients.Caller.SendAsync("command.pending", new { commandId = cmd.Id, data = cmd.Payload });
}
```

### 3.2 Worker MCP Configuration

Worker containers receive an MCP config passed by the spoke during container launch. This config provides access to local resources (Jira, Git, filesystem) that the worker needs. The MCP config is mounted read-only into the container and loaded via `--mcp-config` flag. **The hub never connects to these MCPs** — they are strictly local to the worker container and the spoke.

#### Spoke-Local MCPs (LOCAL ONLY)

Spokes have LOCAL MCP servers that are used by the spoke's own CC instance and worker containers. These are NEVER exposed to the hub.

#### Spoke-Local MCPs

```yaml
# ~/.nexus/.nexus/spoke-mcp-config.json
# These MCPs are LOCAL to the spoke only
{
  "mcpServers": [
    {
      "name": "jira",
      "command": "jira-mcp-server",
      "env": {
        "JIRA_URL": "https://team.atlassian.net",
        "JIRA_USER": "${JIRA_USER}",
        "JIRA_TOKEN": "${JIRA_TOKEN}"
      }
    },
    {
      "name": "git",
      "command": "git-mcp-server",
      "env": {
        "GIT_REPOS_PATH": "${HOME}/.nexus/repos"
      }
    },
    {
      "name": "filesystem",
      "command": "filesystem-mcp-server",
      "env": {
        "ALLOWED_PATHS": "${HOME}/.nexus"
      }
    }
  ]
}
```

The spoke's CC instance loads these via `--mcp-config ./spoke-mcp-config.json` when invoked. These MCPs are strictly local and serve only the spoke's own CC instance.

---

## 4. Spoke Technical Design

### 4.1 Architecture

The spoke is a .NET 10 worker service running as a daemon on each machine. It maintains local state (workspace, memories, projects), handles bidirectional communication with the hub via SignalR, and orchestrates worker containers.

```
Spoke (Worker Service)
├── Program.cs                        # Dependency injection, hosted services
├── Services/
│   ├── HubConnectionService.cs       # WebSocket client, reconnection logic
│   ├── WorkspaceService.cs           # Workspace initialization, file I/O
│   ├── ProjectService.cs             # Project folder creation, metadata
│   ├── JobExecutionService.cs        # Job queuing, container management
│   ├── MemoryService.cs              # Memory read/write/summarization
│   ├── JiraService.cs                # Jira REST API client
│   └── DockerService.cs              # Docker SDK integration
├── Models/
│   ├── Config.cs                     # Config model
│   ├── ProjectMetadata.cs
│   ├── JobDescriptor.cs
│   └── MemoryStore.cs
├── Handlers/
│   ├── JobAssignmentHandler.cs       # Hub → spoke job assignment
│   ├── MessageHandler.cs             # Conversational messages
│   └── DirectiveHandler.cs           # "Work through backlog" type directives
├── Workers/
│   ├── HubConnectionWorker.cs        # Background service for hub connection
│   ├── HeartbeatWorker.cs            # Periodic heartbeat to hub
│   ├── JobQueueWorker.cs             # Job execution queue processor
│   └── OutputStreamWorker.cs         # Stream container output to hub
└── config.yaml                       # Configuration file
```

#### Program.cs Setup

```csharp
using Nexus.Spoke.Services;
using Nexus.Spoke.Workers;
using Microsoft.AspNetCore.SignalR.Client;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var config = new ConfigurationBuilder()
            .AddYamlFile("config.yaml", optional: false)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton(context.Configuration);
        services.AddSingleton<HubConnectionManager>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<ProjectService>();
        services.AddSingleton<JobExecutionService>();
        services.AddSingleton<MemoryService>();
        services.AddSingleton<JiraService>();
        services.AddSingleton<DockerService>();

        services.AddHostedService<HubConnectionWorker>();
        services.AddHostedService<HeartbeatWorker>();
        services.AddHostedService<JobQueueWorker>();

        services.AddLogging(l => l.AddConsole());
    });

var host = builder.Build();
await host.RunAsync();
```

#### Approval Gates Between Job Phases

Approval gates are configured between distinct JOB PHASES (plan → implement → PR), not between individual Claude Code tool calls. The worker container runs with `--permission-mode bypassPermissions`, meaning no tool-call level approval. Instead, the spoke orchestrator checks gates between major execution steps:

- **PlanReview** — After plan phase: user reviews proposed changes before implementation
- **PreExecution** — Before execution: user approves job start (if configured)
- **PostExecution** — After execution: user reviews completed changes before auto-commit/push
- **BatchApproval** — Batch multiple jobs for review before all execute
- **Full Autonomy** — Skip all gates, proceed automatically through all phases

This prevents excessive approval friction while maintaining human oversight at decision points.

#### Config Model (config.yaml)

```yaml
spoke:
  id: "{{ SPOKE_ID }}"                      # Unique ID (UUID)
  name: "Work Laptop"                       # Human-readable name
  capabilities: ["Jira", "Git", "Docker"]   # Available integrations
  max_turns: 50                             # Override hub default per spoke
  timeout_minutes: 30                       # Override hub default per spoke
  profile:                                  # Spoke identity/context
    display_name: "Primary Development Machine"
    machine_description: "Linux workstation with GPU support"
    repos_managed:
      - path: "/home/user/repos/api-service"
        name: "API Service"
      - path: "/home/user/repos/web-app"
        name: "Web Application"
    jira_instances:
      - name: "work"
        url: "https://team.atlassian.net"
        project_keys: ["PROJ", "INFRA"]
    available_integrations:
      - name: "github"
        service: "GitHub"
      - name: "jira"
        service: "Jira"
    description: "Central dev machine, handles most project work"

hub:
  url: "https://nexus.tailnet.com/api/hub"  # Hub WebSocket endpoint
  token: "{{ HUB_TOKEN }}"                   # Pre-shared spoke token

workspace:
  basePath: "/home/user/nexus-spoke"     # Workspace root

jira:
  enabled: true
  instances:
    - name: "work"
      url: "https://team.atlassian.net"
      username: "{{ JIRA_USER }}"
      apiToken: "{{ JIRA_TOKEN }}"
    - name: "personal"
      url: "https://personal.atlassian.net"
      username: "{{ JIRA_USER }}"
      apiToken: "{{ JIRA_TOKEN }}"

git:
  enabled: true
  defaultBranch: "develop"

docker:
  enabled: true
  defaultRegistry: "docker.io"
  workerImage: "nexus/claude-code-worker:latest"
  resourceLimits:
    memory: "4g"
    cpus: "2"

approval:
  mode: "plan_review"  # full_autonomy, plan_review, batch_approval
  batchSize: 5
  max_turns: 50        # Safety limit on agentic turns per job (overrides global)
  timeout_minutes: 30  # Job timeout in minutes (overrides global)

logging:
  level: "Information"
  file: "/home/user/nexus-spoke/logs/spoke.log"
```
pr_monitoring:
  enabled: true
  polling_interval_minutes: 15
  confidence_threshold: 0.8
  monitored_repositories:
    - path: "/home/user/repos/project-alpha"
      enabled: true
    - path: "/home/user/repos/api-service"
      enabled: true
  auto_fix_enabled: true
  respond_to_invalid: true
  escalate_ambiguous: true


```csharp
// Config.cs model
public class SpokeConfig
{
    public SpokeInfo Spoke { get; set; }
    public HubConfig Hub { get; set; }
    public WorkspaceConfig Workspace { get; set; }
    public JiraConfig Jira { get; set; }
    public GitConfig Git { get; set; }
    public DockerConfig Docker { get; set; }
    public ApprovalConfig Approval { get; set; }
    public LoggingConfig Logging { get; set; }
}

public class SpokeInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<string> Capabilities { get; set; }
}

// ... other config classes
```

### 3.2 Hub Connection

#### PSK → JWT Exchange & Refresh

On initial spoke registration, the spoke sends its pre-shared key (PSK) in the Authorization header. The hub validates the PSK, registers the spoke, and returns a spoke-specific JWT (24-hour expiry). For all subsequent SignalR connections, the spoke uses the JWT. The spoke auto-refreshes the JWT by calling a refresh endpoint with the PSK before expiry (max once per 24 hours).

**Spoke Registration Flow:**
1. Spoke starts, reads PSK from config
2. Sends POST /api/spokes/register with `Authorization: Bearer {PSK}`
3. Hub validates PSK, creates spoke, returns JWT
4. Spoke stores JWT in memory, uses for all SignalR connections
5. Before JWT expires (at 20 hours), spoke calls POST /api/auth/refresh with PSK
6. Hub returns new JWT; spoke updates in-memory token

#### HubConnectionManager

```csharp
public class HubConnectionManager
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private readonly string _spokePsk;              // Pre-shared key (from config)
    private string _spokeJwt;                      // Current JWT (from registration)
    private DateTime _jwtExpiresAt;
    private readonly SpokeInfo _spokeInfo;
    private readonly ILogger<HubConnectionManager> _logger;

    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Func<JobAssignmentPayload, Task>? OnJobAssigned;
    public event Func<string, Task>? OnMessageReceived;

    public HubConnectionManager(IConfiguration config, ILogger<HubConnectionManager> logger)
    {
        _hubUrl = config["hub:url"];
        _spokePsk = config["hub:token"];           // PSK from config
        _spokeInfo = config.GetSection("spoke").Get<SpokeInfo>();
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        // Refresh JWT if needed (before connection attempt)
        await RefreshJwtIfExpiredAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_spokeJwt);
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(5),
            })
            .Build();

        _connection.On<JobAssignmentPayload>("job.assign", async (payload) =>
        {
            if (OnJobAssigned != null)
                await OnJobAssigned.Invoke(payload);
        });

        _connection.On<string>("message.to_spoke", async (message) =>
        {
            if (OnMessageReceived != null)
                await OnMessageReceived.Invoke(message);
        });

        _connection.Closed += async (error) =>
        {
            _logger.LogWarning($"Hub connection closed: {error?.Message}");
            OnDisconnected?.Invoke();
        };

        _connection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("Reconnected to hub");
            await RegisterAsync();
            OnConnected?.Invoke();
        };

        try
        {
            await _connection.StartAsync();
            await RegisterAsync();
            OnConnected?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to connect to hub: {ex.Message}");
            throw;
        }
    }

    private async Task RegisterAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("SpokeRegisterAsync", new SpokeRegistrationPayload
        {
            SpokeId = _spokeInfo.Id,
            Name = _spokeInfo.Name,
            Capabilities = _spokeInfo.Capabilities,
        });

        _logger.LogInformation("Spoke registered with hub");
    }

    private async Task RefreshJwtIfExpiredAsync()
    {
        // Refresh JWT if it will expire within the next 5 minutes
        if (DateTime.UtcNow.AddMinutes(5) < _jwtExpiresAt)
            return;  // Still valid for at least 5 minutes

        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_hubUrl.Replace("/api/hub", "")}/api/auth/refresh")
            {
                Headers = { { "Authorization", $"Bearer {_spokePsk}" } }
            };

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                _spokeJwt = json.RootElement.GetProperty("accessToken").GetString();
                _jwtExpiresAt = DateTime.UtcNow.AddHours(24);  // New JWT valid for 24 hours
                _logger.LogInformation("JWT refreshed successfully");
            }
            else
            {
                _logger.LogError($"Failed to refresh JWT: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error refreshing JWT: {ex.Message}");
        }
    }

    public async Task SendHeartbeatAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("SpokeHeartbeatAsync", new SpokeHeartbeatPayload
        {
            Timestamp = DateTime.UtcNow,
        });
    }

    public async Task SendProjectUpdateAsync(Guid projectId, ProjectStatus status)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("ProjectStatusChangedAsync", new ProjectStatusPayload
        {
            ProjectId = projectId,
            Status = status,
        });
    }

    public async Task SendJobStatusAsync(Guid jobId, JobStatus status, string? summary = null)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("JobStatusChangedAsync", new JobStatusPayload
        {
            JobId = jobId,
            Status = status,
            Summary = summary,
        });
    }

    public async Task SendOutputAsync(Guid jobId, string output)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("JobOutputAsync", new JobOutputPayload
        {
            JobId = jobId,
            Output = output,
        });
    }

    public async Task SendMessageAsync(string content)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        await _connection.InvokeAsync("MessageFromSpokeAsync", new MessagePayload
        {
            Content = content,
            Timestamp = DateTime.UtcNow,
        });
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
}
```

#### HubConnectionWorker (Hosted Service)

```csharp
public class HubConnectionWorker : BackgroundService
{
    private readonly HubConnectionManager _hubConnectionManager;
    private readonly ILogger<HubConnectionWorker> _logger;

    public HubConnectionWorker(HubConnectionManager hubConnectionManager, ILogger<HubConnectionWorker> logger)
    {
        _hubConnectionManager = hubConnectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HubConnectionWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_hubConnectionManager.IsConnected)
                {
                    await _hubConnectionManager.ConnectAsync();
                }

                await Task.Delay(5000, stoppingToken); // Check every 5 seconds
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in HubConnectionWorker: {ex.Message}");
                await Task.Delay(10000, stoppingToken); // Backoff on error
            }
        }
    }
}
```

### 4.2 Claude Code Execution Model

Nexus is fundamentally a subprocess orchestrator for Claude Code CLI. All three agent levels (spoke daemon, worker container, hub meta-agent) invoke `claude` as a subprocess with context-specific flags and streaming patterns.

#### 4.2.1 Core Claude Code CLI Reference

**Non-Interactive (Spoke/Worker/Hub):**
```bash
claude -p "prompt text" [flags]        # Print mode: run prompt, exit
claude --bare -p "prompt" [flags]      # Skip auto-discovery (faster)
```

**Resuming Sessions (Multi-Turn):**
```bash
claude --resume <session-id> -p "new message"  # Continue conversation
claude --continue -p "message"                 # Continue most recent session
--fork-session                                 # Branch conversation on resume
```

**Output Control:**
```bash
--output-format text              # Default: plain text (newline-terminated)
--output-format json              # Structured JSON with result, session_id
--output-format stream-json       # Newline-delimited JSON (real-time streaming)
--verbose --include-partial-messages  # Enable token-level streaming
```

**Tool & Permission Control:**
```bash
--allowedTools "Bash,Read,Edit,Write"  # Auto-approve specific tools
--permission-mode acceptEdits          # Accept all edits without prompting
--permission-mode bypassPermissions    # Fully autonomous (no permission gates)
--tools "Bash,Edit,Read"               # Restrict available tools
--max-turns 50                         # Exit after N agentic turns
--max-budget-usd 10.00                 # Exit when spend exceeds limit
```

**Context & System Prompt:**
```bash
--append-system-prompt-file ./skills/CLAUDE.md  # Append skill context
--system-prompt-file ./system.md                # Replace entire system prompt
--mcp-config ./mcp.json                        # Load MCP servers
--plugin-dir /path/to/plugins                  # Load plugin directory
--settings config.json                         # Load settings
```

**Model & Effort:**
```bash
--model claude-sonnet-4-6          # Specify model (default: latest)
--fallback-model opus              # Fallback when primary overloaded
--effort high                       # Cost/speed tradeoff: low|medium|high|max
```

**Session Management:**
```bash
--session-id <uuid>                # Use specific session ID
--name "display name"              # Set session display name
--no-session-persistence           # Don't save session to disk (ephemeral)
```

**Environment:**
```bash
ANTHROPIC_API_KEY=sk-...           # Required (use keychain or env var)
Working directory                  # CC uses cwd for file discovery and context
```

#### 4.2.2 Spoke Agent CC Invocation

The spoke daemon invokes CC to handle user messages and orchestrate work. Each invocation uses `--resume` to maintain conversation context across multiple turns.

**Process Flow:**
1. User sends message via hub UI -> Hub stores in DB -> SignalR broadcasts to spoke
2. Spoke receives message -> builds CC args -> spawns `claude` subprocess -> streams stdout line-by-line -> sends output to hub via SignalR
3. CC processes message (may read files, run commands, fetch Jira, propose changes)
4. After CC exits, spoke stores complete response in memory
5. User can send follow-up message -> spoke resumes session with `--resume`

**Implementation:**

```csharp
// SpokeAgentService.cs

public class SpokeAgentService
{
    private readonly string _ccSessionId;
    private readonly string _spokeWorkspacePath;
    private readonly string _spokeSkillsPath;
    private readonly ILogger<SpokeAgentService> _logger;
    private readonly INexusHubProxy _hubProxy;

    public SpokeAgentService(
        IConfiguration config,
        ILogger<SpokeAgentService> logger,
        INexusHubProxy hubProxy)
    {
        _ccSessionId = config["Spoke:CCSessionId"] ?? Guid.NewGuid().ToString();
        _spokeWorkspacePath = ExpandPath(config["Spoke:WorkspacePath"] ?? "~/.nexus");
        _spokeSkillsPath = Path.Join(_spokeWorkspacePath, ".nexus", "skills");
        _logger = logger;
        _hubProxy = hubProxy;
    }

    /// <summary>
    /// Send a message to the spoke agent via CC, streaming output back to hub.
    /// Maintains conversation state via --resume.
    /// </summary>
    public async Task<string> SendMessageAsync(
        string message,
        string? projectKey = null,
        CancellationToken cancellationToken = default)
    {
        var args = BuildCCArguments(projectKey);
        var process = SpawnCCProcess(args);

        try
        {
            process.Start();

            // Stream output line-by-line back to hub
            var outputBuilder = new StringBuilder();
            _ = process.StandardOutput.BaseStream.CopyToAsync(
                new HubStreamWriter(_hubProxy, _logger), cancellationToken)
                .ConfigureAwait(false);

            // Send user message via stdin
            await process.StandardInput.WriteLineAsync(message);
            process.StandardInput.Close();

            // Wait for completion
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("CC invocation failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);
                throw new SpokeAgentException($"Claude Code failed: {error}");
            }

            return await process.StandardOutput.ReadToEndAsync();
        }
        finally
        {
            process.Dispose();
        }
    }

    private List<string> BuildCCArguments(string? projectKey)
    {
        var args = new List<string>
        {
            "--bare",                          // Skip auto-discovery (faster)
            "-p", "",                          // Will be sent via stdin
            "--resume", _ccSessionId,          // Resume conversation session
            "--output-format", "stream-json",  // Real-time streaming output
            "--verbose",
            "--include-partial-messages",      // Token-level streaming
            "--permission-mode", "acceptEdits", // No human in loop for spoke
            "--max-turns", "30",               // Safety limit
            "--max-budget-usd", "15.00",       // Cost cap per invocation
            "--working-directory", _spokeWorkspacePath,
            "--append-system-prompt-file", Path.Join(_spokeSkillsPath, "CLAUDE.md")
        };

        // Add tool allowlist (MCP + file tools available)
        args.AddRange(new[] { "--allowedTools", "Bash,Read,Edit,Write,Jira,Git" });

        // Add MCP config for spoke (local Jira, Git, file access - LOCAL ONLY)
        var mcpConfigPath = Path.Join(_spokeWorkspacePath, ".nexus", "spoke-mcp-config.json");
        if (File.Exists(mcpConfigPath))
        {
            args.AddRange(new[] { "--mcp-config", mcpConfigPath });
        }

        // Add project-level skills if provided (override spoke skills)
        if (!string.IsNullOrEmpty(projectKey))
        {
            var projectSkillsPath = Path.Join(_spokeWorkspacePath, "projects", projectKey, ".nexus", "skills");
            if (Directory.Exists(projectSkillsPath))
            {
                args.AddRange(new[] { "--plugin-dir", projectSkillsPath });
            }
        }

        // Add spoke-level skills (lower priority)
        if (Directory.Exists(_spokeSkillsPath))
        {
            args.AddRange(new[] { "--plugin-dir", _spokeSkillsPath });
        }

        return args;
    }

    private Process SpawnCCProcess(List<string> args)
    {
        var argString = string.Join(" ", args.Select(a =>
            a.Contains(" ") ? $"\"{a}\"" : a));

        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = argString,
                WorkingDirectory = _spokeWorkspacePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    ["ANTHROPIC_API_KEY"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
                }
            }
        };
    }
}

/// <summary>
/// Stream wrapper that forwards Claude Code output to hub in real-time.
/// Parses stream-json format and sends chunks via SignalR.
/// </summary>
public class HubStreamWriter : Stream
{
    private readonly INexusHubProxy _hub;
    private readonly ILogger _logger;

    public HubStreamWriter(INexusHubProxy hub, ILogger logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var text = Encoding.UTF8.GetString(buffer, offset, count);
        // Parse stream-json events and forward to hub
        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var json = JsonDocument.Parse(line);
                var root = json.RootElement;

                // Extract text delta if present
                if (root.TryGetProperty("event", out var evt) &&
                    evt.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var textProp))
                {
                    _hub.SendStreamOutputAsync(textProp.GetString() ?? "").Wait();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug("Skipped non-JSON line: {Line}", line);
            }
        }
    }

    // Required Stream overrides (not used)
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get; set; }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
```

**Spoke Skills-Based Memory Structure:**

The spoke maintains a skills-based memory system (native Claude Code feature) rather than summarized memory files. This provides better context retention and organization.

- **Spoke workspace root:** `~/.nexus/` (or configured workspace path)
  - `.nexus/skills/CLAUDE.md` — Spoke identity, machine description, repos managed, Jira instances, available integrations, behavioral instructions
  - `.nexus/skills/` — Additional skill files (project templates, integration guides, conventions)
  - `projects/{PROJECT-KEY}/.nexus/skills/CLAUDE.md` — Project-specific context (architecture, tech stack, team conventions)

**Worker Container Skills:**

When the spoke launches a worker container, it generates a temporary `CLAUDE.md` per job:
- Contains task context (job type, ticket, implementation plan, relevant skill pointers)
- Mounted into container with other skills
- Disposed after job completion

**Every CC invocation gets:**
- Full spoke skills directory mounted/available
- Project skills for the specific project (if any)
- Worker-specific context for the current job (in container case)
- Native skill discovery enabled (CC auto-discovers and loads from CLAUDE.md and skill files)

#### 4.2.3 Worker Container CC Invocation

Workers are ephemeral Docker containers spun up by the spoke to execute specific jobs (implement feature, fix bug, review code). Each worker is a fresh CC instance with no session persistence.

**Process Flow:**
1. Spoke receives job from hub -> assembles prompt from job details + project context
2. Spoke mounts workspace, skills, prompt into container
3. Container entrypoint invokes `claude --bare` with full prompt on stdin
4. Worker streams output to stdout -> spoke captures and forwards to hub
5. On exit, spoke parses final result and sends job completion event
6. Container is destroyed

**Worker Entrypoint:**

```bash
#!/bin/bash
# /entrypoint.sh in worker container

set -e

echo "=== Nexus Worker Started ==="
echo "Job: $JOB_TYPE"
echo "Project: $PROJECT_KEY"
echo "Workspace: /workspace/repo"

# Validate mounted files
if [ ! -f /workspace/prompt.md ]; then
    echo "ERROR: Prompt file not mounted at /workspace/prompt.md"
    exit 1
fi

if [ ! -d /workspace/repo ]; then
    echo "ERROR: Repository not mounted at /workspace/repo"
    exit 1
fi

# Build skills path (project overrides spoke)
SKILLS_ARGS=""
if [ -d /workspace/skills/project ]; then
    SKILLS_ARGS="--plugin-dir /workspace/skills/project"
fi
if [ -d /workspace/skills/spoke ]; then
    SKILLS_ARGS="$SKILLS_ARGS --plugin-dir /workspace/skills/spoke"
fi

# Append MCP config if present (local to worker, not exposed to hub)
if [ -f /workspace/spoke-mcp-config.json ]; then
    SKILLS_ARGS="$SKILLS_ARGS --mcp-config /workspace/spoke-mcp-config.json"
fi

# Execute Claude Code with full autonomy
# --bare: skip auto-discovery
# -p: read prompt from stdin (piped below)
# --output-format stream-json: real-time output
# --permission-mode bypassPermissions: no human approval needed
# --max-turns: safety limit
# --max-budget-usd: cost cap
# --no-session-persistence: ephemeral container
echo "=== Invoking Claude Code ==="

cat /workspace/prompt.md | claude \
    --bare \
    -p "$(cat)" \
    --output-format stream-json \
    --verbose \
    --include-partial-messages \
    --permission-mode bypassPermissions \
    --allowedTools "Bash,Read,Edit,Write,Git" \
    --max-turns 50 \
    --max-budget-usd 20.00 \
    --no-session-persistence \
    --append-system-prompt-file /workspace/skills/system.md \
    $SKILLS_ARGS \
    --working-directory /workspace/repo

echo "=== Worker Completed Successfully ==="
exit 0
```

**Spoke -> Worker Invocation (C#):**

```csharp
// SpokeDockerService.cs

public class SpokeDockerService
{
    private readonly DockerClient _docker;
    private readonly string _workerImage;
    private readonly ILogger<SpokeDockerService> _logger;
    private readonly INexusHubProxy _hubProxy;

    public async Task<JobResult> ExecuteJobAsync(
        Job job,
        Project project,
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        // Assemble prompt from job details
        var prompt = AssemblePrompt(job, project);
        var promptFile = Path.Join(Path.GetTempPath(), $"prompt-{job.Id}.md");
        await File.WriteAllTextAsync(promptFile, prompt, cancellationToken);

        // Prepare container mounts
        var containerName = $"nexus-worker-{job.Id:N}";
        var mounts = new List<Mount>
        {
            // Repository (read-write for commits/pushes)
            new Mount
            {
                Type = "bind",
                Source = repoPath,
                Target = "/workspace/repo",
                ReadOnly = false
            },
            // Prompt file (read-only)
            new Mount
            {
                Type = "bind",
                Source = promptFile,
                Target = "/workspace/prompt.md",
                ReadOnly = true
            },
            // Output directory
            new Mount
            {
                Type = "bind",
                Source = Path.Join(repoPath, ".nexus", "output", job.Id.ToString()),
                Target = "/workspace/output",
                ReadOnly = false
            }
        };

        // Mount spoke-level skills
        var spokeSkillsPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nexus", ".nexus", "skills");
        if (Directory.Exists(spokeSkillsPath))
        {
            mounts.Add(new Mount
            {
                Type = "bind",
                Source = spokeSkillsPath,
                Target = "/workspace/skills/spoke",
                ReadOnly = true
            });
        }

        // Mount project-level skills (override)
        var projectSkillsPath = Path.Join(repoPath, ".nexus", "skills");
        if (Directory.Exists(projectSkillsPath))
        {
            mounts.Add(new Mount
            {
                Type = "bind",
                Source = projectSkillsPath,
                Target = "/workspace/skills/project",
                ReadOnly = true
            });
        }

        // Mount MCP config
        var mcpConfigPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nexus", ".nexus", "mcp-config.json");
        if (File.Exists(mcpConfigPath))
        {
            mounts.Add(new Mount
            {
                Type = "bind",
                Source = mcpConfigPath,
                Target = "/workspace/mcp-config.json",
                ReadOnly = true
            });
        }

        // Create container
        var createResp = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = _workerImage,
                Name = containerName,
                Hostname = containerName,
                Env = new List<string>
                {
                    $"JOB_ID={job.Id}",
                    $"JOB_TYPE={job.Type}",
                    $"PROJECT_KEY={project.Key}",
                    $"ANTHROPIC_API_KEY={Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")}"
                },
                HostConfig = new HostConfig
                {
                    Mounts = mounts,
                    Memory = 2147483648,  // 2GB
                    CpuShares = 1024      // 1 CPU
                }
            },
            cancellationToken);

        try
        {
            // Start container
            await _docker.Containers.StartContainerAsync(containerName, new ContainerStartParameters(),
                cancellationToken);

            _logger.LogInformation("Container {ContainerName} started for job {JobId}", containerName, job.Id);

            // Attach to container and stream output to hub
            var outputBuilder = new StringBuilder();
            using var stream = await _docker.Containers.AttachContainerAsync(
                containerName,
                new ContainerAttachParameters { Stream = true, Stdout = true, Stderr = true },
                cancellationToken);

            // Forward output to hub in real-time
            var streamTask = stream.CopyOutputToAsync(null, null,
                new Progress<string>(chunk =>
                {
                    outputBuilder.Append(chunk);
                    _hubProxy.SendJobOutputAsync(job.Id, chunk).ConfigureAwait(false).GetAwaiter().GetResult();
                }),
                cancellationToken);

            // Wait for container to exit
            var waitResp = await _docker.Containers.WaitContainerAsync(containerName, cancellationToken);

            _logger.LogInformation("Container {ContainerName} exited with code {ExitCode}",
                containerName, waitResp.StatusCode);

            // Parse output and extract result
            var output = outputBuilder.ToString();
            var result = ParseWorkerOutput(output, job);

            return result;
        }
        finally
        {
            // Clean up container
            try
            {
                await _docker.Containers.RemoveContainerAsync(containerName,
                    new ContainerRemoveParameters { Force = true }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to remove container {ContainerName}: {Error}",
                    containerName, ex.Message);
            }

            // Clean up temp files
            try { File.Delete(promptFile); } catch { }
        }
    }

    private string AssemblePrompt(Job job, Project project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Nexus Job: {job.Type}");
        sb.AppendLine($"Project: {project.Name} ({project.Key})");
        sb.AppendLine($"Job ID: {job.Id}");
        sb.AppendLine();
        sb.AppendLine(job.Description);
        sb.AppendLine();
        sb.AppendLine("## Context");
        // Append project structure, recent commits, linked Jira ticket, etc.
        if (!string.IsNullOrEmpty(job.Context))
        {
            sb.AppendLine(job.Context);
        }

        return sb.ToString();
    }

    private JobResult ParseWorkerOutput(string output, Job job)
    {
        // Parse stream-json output and extract final result
        // Look for structured result at the end of output
        var lines = output.Split('\n');
        var lastJsonLine = lines.Reverse()
            .FirstOrDefault(l => l.TrimStart().StartsWith("{"));

        if (string.IsNullOrEmpty(lastJsonLine))
        {
            return new JobResult
            {
                JobId = job.Id,
                Status = JobStatus.Failed,
                Summary = "No structured output from worker",
                Output = output
            };
        }

        try
        {
            var resultJson = JsonDocument.Parse(lastJsonLine);
            var root = resultJson.RootElement;

            return new JobResult
            {
                JobId = job.Id,
                Status = root.TryGetProperty("status", out var s) &&
                         s.GetString() == "success" ? JobStatus.Completed : JobStatus.Failed,
                Summary = root.TryGetProperty("summary", out var sum) ? sum.GetString() : "",
                Output = output,
                FilesChanged = root.TryGetProperty("files_changed", out var files)
                    ? files.EnumerateArray().Select(f => f.GetString()).ToList() : new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to parse worker output: {Error}", ex.Message);
            return new JobResult
            {
                JobId = job.Id,
                Status = JobStatus.Failed,
                Summary = $"Parse error: {ex.Message}",
                Output = output
            };
        }
    }
}
```

#### 4.2.4 Hub Meta-Agent CC Invocation

The hub meta-agent is a CC instance running on the hub server that coordinates across spokes, makes decisions about job routing, and synthesizes status reports.

**Hub Agent Purpose:**
- Receive "orchestrate work on feature X" from user
- Call spoke agents via MCP (pull project structure, recent issues)
- Generate execution plan
- Create jobs on appropriate spokes based on capacity/expertise
- Monitor job status, provide feedback to user

**Invocation Pattern:**

```csharp
// HubAgentService.cs

public class HubAgentService
{
    private readonly string _hubWorkspacePath;
    private readonly ILogger<HubAgentService> _logger;
    private readonly INexusHubProxy _hubProxy;

    public async Task<string> ExecuteOrchestratorPromptAsync(
        string userRequest,
        List<Spoke> availableSpokes,
        string? focusProjectKey = null)
    {
        // Build context about available spokes and projects
        var spokeContext = BuildSpokeContext(availableSpokes);

        // Assemble system prompt for hub agent (hub-specific skills)
        var systemPromptPath = Path.Join(_hubWorkspacePath, ".nexus", "skills", "hub-CLAUDE.md");
        var systemPrompt = File.Exists(systemPromptPath)
            ? await File.ReadAllTextAsync(systemPromptPath)
            : DefaultHubSystemPrompt();

        // Invoke Claude Code for orchestration
        var args = new List<string>
        {
            "--bare",
            "-p", userRequest,
            "--output-format", "stream-json",
            "--verbose",
            "--permission-mode", "acceptEdits",
            "--max-turns", "20",
            "--max-budget-usd", "25.00",
            "--system-prompt", systemPrompt,
            "--append-system-prompt", spokeContext,
            "--working-directory", _hubWorkspacePath
        };

        // Hub agent uses hub-local tools (NOT spoke MCPs) to query spokes via SignalR
        var hubMcpConfig = Path.Join(_hubWorkspacePath, ".nexus", "hub-mcp-config.json");
        if (File.Exists(hubMcpConfig))
        {
            args.AddRange(new[] { "--mcp-config", hubMcpConfig });
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = string.Join(" ", args),
                WorkingDirectory = _hubWorkspacePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    ["ANTHROPIC_API_KEY"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
                }
            }
        };

        var output = new StringBuilder();
        try
        {
            process.Start();

            // Stream output to connected clients
            var readTask = process.StandardOutput.ReadLineAsync();
            while (readTask != null)
            {
                var line = await readTask;
                if (string.IsNullOrEmpty(line)) break;

                output.AppendLine(line);
                await _hubProxy.BroadcastAgentOutputAsync(line);
                readTask = process.StandardOutput.ReadLineAsync();
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Hub agent failed: {Error}", error);
            }

            return output.ToString();
        }
        finally
        {
            process.Dispose();
        }
    }

    private string BuildSpokeContext(List<Spoke> spokes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n## Available Spokes\n");

        foreach (var spoke in spokes)
        {
            sb.AppendLine($"- **{spoke.Name}** (ID: {spoke.Id})");
            sb.AppendLine($"  - OS: {spoke.OS} / {spoke.Architecture}");
            sb.AppendLine($"  - Status: {spoke.Status}");
            sb.AppendLine($"  - Capabilities: {string.Join(", ", spoke.Capabilities)}");
            sb.AppendLine($"  - Projects: {string.Join(", ", spoke.ProjectKeys)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string DefaultHubSystemPrompt() =>
        @"You are the Nexus Hub orchestrator. Your role is to:
1. Understand user requests for distributed work
2. Analyze available spokes and their capabilities
3. Decompose large tasks into jobs for specific spokes
4. Create jobs via Jira and route them
5. Synthesize status updates from multiple spokes
6. Resolve conflicts when job execution fails

Always explain your reasoning before creating jobs.
Use available hub-local tools to query spoke agents (via SignalR) and fetch project context from the hub database.";
}
```

#### 4.2.5 Streaming Output Architecture

All three invocation patterns (spoke, worker, hub) use stream-json output format for real-time feedback to the UI.

**Stream-JSON Format:**
```json
{"type": "stream_event", "event": {"delta": {"type": "text_delta", "text": "partial output..."}}}
{"type": "stream_event", "event": {"delta": {"type": "text_delta", "text": " more text"}}}
{"type": "system", "subtype": "api_retry", "attempt": 1, "backoff_ms": 1000}
{"type": "completion", "result": {"status": "success", "session_id": "uuid", ...}}
```

**Hub SignalR Relay:**
```csharp
// In spoke agent service or worker stream handler
foreach (var line in streamOutput.Split('\n'))
{
    if (string.IsNullOrWhiteSpace(line)) continue;

    try
    {
        var json = JsonDocument.Parse(line);
        var root = json.RootElement;

        if (root.TryGetProperty("event", out var evt) &&
            evt.TryGetProperty("delta", out var delta) &&
            delta.TryGetProperty("text", out var text))
        {
            // Send text chunk to hub via SignalR
            await _hubProxy.SendStreamOutputAsync(jobId, text.GetString());
        }
        else if (root.TryGetProperty("type", out var type) &&
                 type.GetString() == "system" &&
                 root.TryGetProperty("subtype", out var subtype) &&
                 subtype.GetString() == "api_retry")
        {
            // Handle rate limit retry
            _logger.LogWarning("CC rate limited, retrying...");
        }
    }
    catch (JsonException)
    {
        // Non-JSON line, log as plain text
        await _hubProxy.SendStreamOutputAsync(jobId, line);
    }
}
```

#### 4.2.6 Error Handling & Timeouts

```csharp
public async Task<JobResult> ExecuteWithTimeoutAsync(
    Func<CancellationToken, Task<JobResult>> executor,
    TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);

    try
    {
        return await executor(cts.Token);
    }
    catch (OperationCanceledException)
    {
        return new JobResult
        {
            Status = JobStatus.Failed,
            Summary = $"Job exceeded timeout of {timeout.TotalMinutes} minutes",
            Output = "TIMEOUT"
        };
    }
    catch (Exception ex) when (ex is not SpokeAgentException)
    {
        _logger.LogError(ex, "Unexpected error during job execution");
        return new JobResult
        {
            Status = JobStatus.Failed,
            Summary = ex.Message,
            Output = ex.StackTrace ?? ""
        };
    }
}
```

**Retry Strategy for Rate Limits:**
- Parse stream-json for `api_retry` events
- Max 3 retries per job invocation
- Exponential backoff (1s, 2s, 4s)
- After 3 failures, mark job as requiring human review

### 4.3 Jira Integration

#### JiraService

```csharp
public class JiraService
{
    private readonly Dictionary<string, JiraClient> _clients;
    private readonly ILogger<JiraService> _logger;

    public JiraService(IConfiguration config, ILogger<JiraService> logger)
    {
        _logger = logger;
        _clients = new Dictionary<string, JiraClient>();

        var jiraConfig = config.GetSection("jira").Get<JiraConfig>();
        if (jiraConfig?.Enabled == true)
        {
            foreach (var instance in jiraConfig.Instances)
            {
                var client = new JiraClient(instance.Url, instance.Username, instance.ApiToken);
                _clients[instance.Name] = client;
            }
        }
    }

    public async Task<JiraTicket> GetTicketAsync(string instance, string ticketKey)
    {
        if (!_clients.TryGetValue(instance, out var client))
            throw new KeyNotFoundException($"Jira instance '{instance}' not configured");

        var response = await client.GetAsync($"/rest/api/3/issues/{ticketKey}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch ticket {ticketKey}");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JiraTicket>(json)!;
    }

    public async Task<List<JiraTicket>> GetSprintBacklogAsync(string instance, string boardId)
    {
        if (!_clients.TryGetValue(instance, out var client))
            throw new KeyNotFoundException($"Jira instance '{instance}' not configured");

        var response = await client.GetAsync($"/rest/api/3/boards/{boardId}/backlog");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch backlog for board {boardId}");

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(json).RootElement;
        var issues = data.GetProperty("issues").EnumerateArray()
            .Select(i => JsonSerializer.Deserialize<JiraTicket>(i.GetRawText())!)
            .ToList();

        return issues;
    }
}

public class JiraTicket
{
    public string Key { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string Status { get; set; }
    public string Assignee { get; set; }
    public List<string> Labels { get; set; }
    public string Priority { get; set; }
}

public class JiraClient : HttpClient
{
    public JiraClient(string baseUrl, string username, string token)
    {
        BaseAddress = new Uri(baseUrl);
        DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{token}"))}");
    }
}
```

### 4.4 Docker Integration

#### DockerService

```csharp
public class DockerService
{
    private readonly DockerClient _dockerClient;
    private readonly string _workerImage;
    private readonly ResourceLimits _resourceLimits;
    private readonly ILogger<DockerService> _logger;

    public DockerService(IConfiguration config, ILogger<DockerService> logger)
    {
        _logger = logger;
        _dockerClient = new DockerClientConfiguration().CreateClient();

        var dockerConfig = config.GetSection("docker").Get<DockerConfig>();
        _workerImage = dockerConfig.WorkerImage;
        _resourceLimits = dockerConfig.ResourceLimits;
    }

    public async Task<ContainerExecutionResult> ExecuteJobAsync(
        Guid jobId,
        string repoPath,
        string promptFile,
        string outputDirectory,
        Func<string, Task> onOutput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure image is available
            await PullImageAsync(_workerImage);

            // Create container
            var containerName = $"nexus-job-{jobId:N}";
            var createResponse = await _dockerClient.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = _workerImage,
                    Name = containerName,
                    AttachStdout = true,
                    AttachStderr = true,
                    Hostname = containerName,
                    Volumes = new Dictionary<string, EmptyStruct>
                    {
                        { repoPath, new EmptyStruct() },
                        { promptFile, new EmptyStruct() },
                        { outputDirectory, new EmptyStruct() },
                    },
                    HostConfig = new HostConfig
                    {
                        Binds = new[]
                        {
                            $"{repoPath}:/workspace/repo",
                            $"{promptFile}:/workspace/prompt.md:ro",
                            $"{outputDirectory}:/workspace/output",
                        },
                        Memory = _resourceLimits.MemoryBytes,
                        CpuQuota = (long)(_resourceLimits.Cpus * 100000),
                        AutoRemove = true,
                    },
                    Entrypoint = new[] { "/bin/bash", "-c" },
                    Cmd = new[] { "claude code /workspace/prompt.md" },
                },
                cancellationToken);

            var containerId = createResponse.ID;
            _logger.LogInformation($"Created container {containerName} for job {jobId}");

            // Start container
            await _dockerClient.Containers.StartContainerAsync(
                containerId,
                new ContainerStartParameters(),
                cancellationToken);

            // Attach to container and stream output
            var outputStream = await _dockerClient.Containers.AttachContainerAsync(
                containerId,
                new ContainerAttachParameters
                {
                    Stream = true,
                    Stdout = true,
                    Stderr = true,
                },
                cancellationToken);

            var multiplexStream = new MultiplexedStream(outputStream);
            using var reader = new StreamReader(multiplexStream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                await onOutput(line + Environment.NewLine);
            }

            // Wait for container to finish
            var waitResponse = await _dockerClient.Containers.WaitContainerAsync(
                containerId,
                cancellationToken);

            _logger.LogInformation($"Container {containerName} exited with code {waitResponse.StatusCode}");

            return new ContainerExecutionResult
            {
                ExitCode = waitResponse.StatusCode,
                Success = waitResponse.StatusCode == 0,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing job {jobId}: {ex.Message}");
            await onOutput($"ERROR: {ex.Message}");
            throw;
        }
    }

    private async Task PullImageAsync(string image)
    {
        try
        {
            await _dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                new Progress<JSONMessage>());

            _logger.LogInformation($"Pulled image {image}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to pull image {image}: {ex.Message}. Using cached version if available.");
        }
    }
}

public class ResourceLimits
{
    public string Memory { get; set; } // "4g", "512m", etc.
    public double Cpus { get; set; }   // 2.0, 0.5, etc.

    public long MemoryBytes => ParseMemory(Memory);
    public static long ParseMemory(string memory)
    {
        var unit = memory.LastOrDefault();
        var value = long.Parse(memory[..^1]);
        return unit switch
        {
            'k' or 'K' => value * 1000,
            'm' or 'M' => value * 1_000_000,
            'g' or 'G' => value * 1_000_000_000,
            _ => value,
        };
    }
}

public class ContainerExecutionResult
{
    public long ExitCode { get; set; }
    public bool Success { get; set; }
}
```

### 4.5 Memory System

The memory system is file-based, human-readable markdown with structured sections.

#### MemoryService

```csharp
public class MemoryService
{
    private readonly string _memoryPath;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(IConfiguration config, ILogger<MemoryService> logger)
    {
        _memoryPath = Path.Combine(config["workspace:basePath"], ".nexus", "memories");
        _logger = logger;

        // Ensure memory directory exists
        Directory.CreateDirectory(_memoryPath);
    }

    public async Task<string> ReadGlobalMemoryAsync()
    {
        var path = Path.Combine(_memoryPath, "global.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    public async Task AppendToGlobalMemoryAsync(string content)
    {
        var path = Path.Combine(_memoryPath, "global.md");
        await File.AppendAllTextAsync(path, $"\n\n{content}\n");
        _logger.LogInformation("Updated global memory");
    }

    public async Task<string> ReadCodebaseNotesAsync()
    {
        var path = Path.Combine(_memoryPath, "codebase-notes.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    public async Task<string> ReadDecisionLogAsync()
    {
        var path = Path.Combine(_memoryPath, "decision-log.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    public async Task<string> ReadProjectContextAsync(Guid projectId)
    {
        var path = Path.Combine(_memoryPath, "projects", projectId.ToString(), "context.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    public async Task WriteProjectContextAsync(Guid projectId, string content)
    {
        var dir = Path.Combine(_memoryPath, "projects", projectId.ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "context.md");
        await File.WriteAllTextAsync(path, content);
    }

    // Summarize memory when it gets too large (>100KB)
    public async Task SummarizeMemoryAsync()
    {
        var globalMemoryPath = Path.Combine(_memoryPath, "global.md");
        if (!File.Exists(globalMemoryPath))
            return;

        var fileInfo = new FileInfo(globalMemoryPath);
        if (fileInfo.Length < 100_000) // Less than 100KB
            return;

        _logger.LogInformation("Summarizing global memory...");
        var content = await File.ReadAllTextAsync(globalMemoryPath);

        // Use Claude API to summarize (sketch; actual implementation would call Claude)
        var summary = await SummarizeWithClaudeAsync(content);

        // Keep summary + recent entries
        var recent = content.Split("\n\n").TakeLast(10).Join("\n\n");
        var newContent = $"# Global Memory Summary\n\n{summary}\n\n# Recent Entries\n\n{recent}";

        await File.WriteAllTextAsync(globalMemoryPath, newContent);
    }

    private async Task<string> SummarizeWithClaudeAsync(string content)
    {
        // Call Claude API with summarization prompt
        // This is a sketch; real implementation would use Anthropic SDK
        return "TODO: Implement Claude summarization";
    }
}
```

---


### 4.6 PR Monitoring Service

The spoke monitors open pull requests for actionable feedback and performs automatic fixes or escalations. This service runs on a configurable polling interval (default 15 minutes).

#### Architecture

- **IGitProvider** — Abstract interface for Git platform APIs (GitHub, GitLab)
- **GitHubProvider** — GitHub API implementation
- **GitLabProvider** — GitLab API implementation
- **PullRequestMonitor** — Background service that polls for PR comments on configured intervals
- **CommentClassifier** — Uses Claude API to classify comments as actionable, invalid, positive, or ambiguous
- **AutoFixExecutor** — Creates fix jobs for actionable feedback and routes responses through hub pending actions
- **Configuration** — Enabled/disabled per spoke, with per-repo inclusions, confidence thresholds, and polling intervals

#### IGitProvider Interface

```csharp
public interface IGitProvider
{
    Task<List<PullRequest>> GetOpenPullRequestsAsync(string repoPath);
    Task<List<PullRequestComment>> GetUnprocessedCommentsAsync(string repoPath, string prNumber);
    Task PostCommentReplyAsync(string repoPath, string prNumber, string commentId, string reply);
    Task<bool> CanAccessAsync(string repoPath); // Verify credentials are available
    string ProviderType { get; }  // "github" or "gitlab"
}

public class PullRequest
{
    public string Number { get; set; }
    public string Title { get; set; }
    public string Branch { get; set; }
    public string BaseRef { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PullRequestComment
{
    public string Id { get; set; }
    public string Author { get; set; }
    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FileContext { get; set; } // Code snippet the comment refers to
    public string ParentCommentId { get; set; } // For threaded conversations
}
```

#### Git Provider Implementations

**GitHub:**

```csharp
public class GitHubProvider : IGitProvider
{
    private readonly GitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;

    public string ProviderType => "github";

    public GitHubProvider(string repoPath, string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("NexusSpoke"))
        {
            Credentials = new Credentials(token)
        };

        // Parse owner/repo from repoPath or use git config
        (_owner, _repo) = ParseGitRepo(repoPath);
    }

    public async Task<List<PullRequest>> GetOpenPullRequestsAsync(string repoPath)
    {
        var prs = await _client.PullRequest.GetAllAsync(_owner, _repo,
            new PullRequestRequest { State = ItemStateFilter.Open });

        return prs.Select(pr => new PullRequest
        {
            Number = pr.Number.ToString(),
            Title = pr.Title,
            Branch = pr.Head.Ref,
            BaseRef = pr.Base.Ref,
            CreatedAt = pr.CreatedAt.DateTime,
        }).ToList();
    }

    public async Task<List<PullRequestComment>> GetUnprocessedCommentsAsync(string repoPath, string prNumber)
    {
        var comments = await _client.PullRequest.ReviewComment.GetAllAsync(_owner, _repo, int.Parse(prNumber));
        
        var unprocessed = comments
            .Where(c => !c.Body.Contains("<!-- nexus-processed -->"))
            .Select(c => new PullRequestComment
            {
                Id = c.Id.ToString(),
                Author = c.User.Login,
                Body = c.Body,
                CreatedAt = c.CreatedAt.DateTime,
                FileContext = c.DiffHunk,
            })
            .ToList();

        return unprocessed;
    }

    public async Task PostCommentReplyAsync(string repoPath, string prNumber, string commentId, string reply)
    {
        await _client.PullRequest.ReviewComment.ReplyAsync(_owner, _repo, int.Parse(prNumber),
            long.Parse(commentId), reply);
    }

    public async Task<bool> CanAccessAsync(string repoPath)
    {
        try
        {
            var repo = await _client.Repository.Get(_owner, _repo);
            return repo != null;
        }
        catch
        {
            return false;
        }
    }

    private (string owner, string repo) ParseGitRepo(string repoPath)
    {
        // Extract from git remote origin URL or path
        // e.g., /path/to/repos/owner/repo → ("owner", "repo")
        var parts = repoPath.TrimEnd('/').Split(Path.DirectorySeparatorChar);
        return (parts[^2], parts[^1]);
    }
}
```

**GitLab:**

```csharp
public class GitLabProvider : IGitProvider
{
    private readonly GitLabClient _client;
    private readonly string _projectId;

    public string ProviderType => "gitlab";

    public GitLabProvider(string repoPath, string token)
    {
        _client = new GitLabClient(token);
        _projectId = ExtractProjectId(repoPath);
    }

    public async Task<List<PullRequest>> GetOpenPullRequestsAsync(string repoPath)
    {
        // GitLab calls PRs "Merge Requests"
        var mrs = await _client.Projects[_projectId].MergeRequests.GetAsync(
            new QueryOptions { State = "opened" });

        return mrs.Select(mr => new PullRequest
        {
            Number = mr.Iid.ToString(),
            Title = mr.Title,
            Branch = mr.SourceBranch,
            BaseRef = mr.TargetBranch,
            CreatedAt = mr.CreatedAt,
        }).ToList();
    }

    public async Task<List<PullRequestComment>> GetUnprocessedCommentsAsync(string repoPath, string prNumber)
    {
        var notes = await _client.Projects[_projectId].MergeRequests[int.Parse(prNumber)].Notes.GetAsync();

        var unprocessed = notes
            .Where(n => !n.Body.Contains("<!-- nexus-processed -->"))
            .Select(n => new PullRequestComment
            {
                Id = n.Id.ToString(),
                Author = n.Author.Username,
                Body = n.Body,
                CreatedAt = n.CreatedAt,
                FileContext = "",  // GitLab notes don't include diff context in same way
                ParentCommentId = n.InReplyToNote?.Id.ToString()
            })
            .ToList();

        return unprocessed;
    }

    public async Task PostCommentReplyAsync(string repoPath, string prNumber, string commentId, string reply)
    {
        await _client.Projects[_projectId].MergeRequests[int.Parse(prNumber)].Notes.CreateAsync(
            new Note { Body = reply, InReplyToNoteId = int.Parse(commentId) });
    }

    public async Task<bool> CanAccessAsync(string repoPath)
    {
        try
        {
            var project = await _client.Projects[_projectId].GetAsync();
            return project != null;
        }
        catch
        {
            return false;
        }
    }

    private string ExtractProjectId(string repoPath)
    {
        // Extract GitLab project ID from git remote or derive from path
        // Implementation depends on how project IDs are managed
        return repoPath.TrimEnd('/').GetHashCode().ToString();
    }
}
```

#### Comment Classification

```csharp
public class CommentClassifier
{
    private readonly IAnthropicClient _anthropic;
    private readonly ILogger<CommentClassifier> _logger;

    public enum CommentType
    {
        Actionable,    // "Add null check", "Missing transaction", "Use regex"
        Invalid,       // "Why not use X?" when X doesn't apply, misunderstanding
        Positive,      // "Looks good", "Nice approach", "LGTM"
        Ambiguous      // Needs human judgment; unclear intent or requires contextual knowledge
    }

    public class Classification
    {
        public CommentType Type { get; set; }
        public double Confidence { get; set; } // 0.0 to 1.0
        public string Reasoning { get; set; }
        public string SuggestedAction { get; set; } // For Actionable comments
    }

    public async Task<Classification> ClassifyAsync(PullRequestComment comment, string codeContext)
    {
        var prompt = $@"Classify this pull request review comment as one of: Actionable, Invalid, Positive, or Ambiguous.

Comment: {comment.Body}

Code Context (what the comment refers to):
{codeContext}

Requirements:
- Actionable: Specific feedback we can implement (e.g., 'Add null check', 'Missing transaction', 'Use try-catch')
- Invalid: Misunderstanding or approach doesn't apply to this codebase (e.g., 'Why not use async?' when sync is intentional)
- Positive: Approval-type comments (e.g., 'LGTM', 'Looks good', 'Nice approach')
- Ambiguous: Unclear intent, context-dependent, or requires human judgment

Respond with JSON:
{{
  ""type"": ""Actionable|Invalid|Positive|Ambiguous"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""Brief explanation"",
  ""suggested_action"": ""If Actionable, describe the fix. Otherwise null.""
}}";

        var response = await _anthropic.Messages.CreateAsync(new MessageCreateRequest
        {
            Model = "claude-opus-4-1",
            MaxTokens = 500,
            Messages = new[] { new ContentBlockParam { Type = "text", Text = prompt } }
        });

        var json = ExtractJSON(response.Content[0].Text);
        var result = JsonSerializer.Deserialize<Classification>(json);
        
        _logger.LogInformation($"Classified PR comment: {result.Type} (confidence {result.Confidence})");
        return result;
    }
}
```

#### Pull Request Monitor (Background Service)

```csharp
public class PullRequestMonitorWorker : BackgroundService
{
    private readonly SpokeConfig _config;
    private readonly IPullRequestProvider _prProvider;
    private readonly CommentClassifier _classifier;
    private readonly JobExecutionService _jobExecutor;
    private readonly HubConnectionManager _hubConnection;
    private readonly ILogger<PullRequestMonitorWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prConfig = _config.PrMonitoring;
        if (!prConfig.Enabled)
        {
            _logger.LogInformation("PR Monitoring disabled in config");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorOpenPullRequestsAsync();
                await Task.Delay(TimeSpan.FromMinutes(prConfig.PollingIntervalMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error monitoring PRs: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task MonitorOpenPullRequestsAsync()
    {
        var prConfig = _config.PrMonitoring;

        foreach (var monitoredRepo in prConfig.MonitoredRepositories)
        {
            if (!await _prProvider.CanAccessAsync(monitoredRepo.Path))
            {
                _logger.LogWarning($"Cannot access repo {monitoredRepo.Path}; skipping");
                continue;
            }

            var prs = await _prProvider.GetOpenPullRequestsAsync(monitoredRepo.Path);
            _logger.LogInformation($"Found {prs.Count} open PRs in {monitoredRepo.Path}");

            foreach (var pr in prs)
            {
                var comments = await _prProvider.GetUnprocessedCommentsAsync(monitoredRepo.Path, pr.Number);
                _logger.LogInformation($"PR {pr.Number}: {comments.Count} unprocessed comments");

                foreach (var comment in comments)
                {
                    var classification = await _classifier.ClassifyAsync(comment, pr.Title);

                    if (classification.Confidence < prConfig.ConfidenceThreshold && classification.Type != CommentType.Positive)
                    {
                        // Low confidence: escalate to user
                        await CreatePendingActionAsync(pr, comment, classification);
                    }
                    else
                    {
                        switch (classification.Type)
                        {
                            case CommentType.Actionable:
                                await HandleActionableCommentAsync(monitoredRepo.Path, pr, comment, classification);
                                break;

                            case CommentType.Invalid:
                                await RespondToInvalidCommentAsync(pr, comment, classification);
                                break;

                            case CommentType.Positive:
                                _logger.LogInformation($"Positive comment on PR {pr.Number}; no action needed");
                                break;

                            case CommentType.Ambiguous:
                                await CreatePendingActionAsync(pr, comment, classification);
                                break;
                        }
                    }

                    // Mark as processed to avoid reprocessing
                    var markedReply = $"{comment.Body}\n\n<!-- nexus-processed -->";
                    await _prProvider.PostCommentReplyAsync(monitoredRepo.Path, pr.Number, comment.Id, markedReply);
                }
            }
        }
    }

    private async Task HandleActionableCommentAsync(string repoPath, PullRequest pr, PullRequestComment comment, CommentClassifier.Classification classification)
    {
        // Create a fix job
        var jobId = Guid.NewGuid();
        var fixPrompt = $@"
# Fix PR Comment

PR: {pr.Title} ({pr.Branch} → {pr.BaseRef})
Comment: {comment.Body}
Context: {comment.FileContext}

Action Required:
{classification.SuggestedAction}

Please implement the fix and push to the PR branch.
";

        await _jobExecutor.QueueJobAsync(new JobDescriptor
        {
            Id = jobId,
            Type = JobType.Custom,
            Prompt = fixPrompt,
            Repository = repoPath,
        });

        // Notify hub and user
        await _hubConnection.SendMessageAsync($"Auto-created fix job {jobId} for PR {pr.Number} comment from {comment.Author}");
        _logger.LogInformation($"Created fix job {jobId} for PR {pr.Number}");
    }

    private async Task RespondToInvalidCommentAsync(string repoPath, PullRequest pr, PullRequestComment comment, CommentClassifier.Classification classification)
    {
        // Generate proposed response
        var proposedReply = $@"Thanks for the review! {classification.Reasoning}

Our approach is intentional here. Would you like to discuss further?";

        // Create pending action for user to review/approve before posting
        var action = new PendingAction
        {
            Id = Guid.NewGuid(),
            SpokeId = _spokeId,
            ProjectId = _projectId,
            GateType = "pr_review_response",
            Summary = $"Review response for PR {pr.Number} comment from {comment.Author}",
            Description = $"Comment:\n{comment.Body}\n\nProposed Response:\n{proposedReply}",
            Metadata = new Dictionary<string, object>
            {
                { "repository_path", repoPath },
                { "pr_number", pr.Number },
                { "comment_id", comment.Id },
                { "parent_comment_id", comment.ParentCommentId },  // For threading
                { "proposed_reply", proposedReply },
                { "classification_type", classification.Type },
                { "confidence", classification.Confidence }
            },
            CreatedAt = DateTime.UtcNow
        };

        // Send to hub as pending action; user approves/rejects before posting
        await _hubConnection.SendPendingActionAsync(action);
        _logger.LogInformation($"Created pending action (response approval) for PR {pr.Number} comment");
    }

    private async Task CreatePendingActionAsync(string repoPath, PullRequest pr, PullRequestComment comment, CommentClassifier.Classification classification)
    {
        // Create pending action for ambiguous or low-confidence comments
        var action = new PendingAction
        {
            Id = Guid.NewGuid(),
            SpokeId = _spokeId,
            ProjectId = _projectId,
            GateType = "pr_review",
            Summary = $"PR {pr.Number}: Review comment from {comment.Author}",
            Description = $"Comment:\n{comment.Body}\n\nClassified as: {classification.Type} (confidence: {classification.Confidence:P})\nReasoning: {classification.Reasoning}",
            Metadata = new Dictionary<string, object>
            {
                { "repository_path", repoPath },
                { "pr_number", pr.Number },
                { "comment_id", comment.Id },
                { "parent_comment_id", comment.ParentCommentId },  // Track parent for threading
                { "file_context", comment.FileContext }
            },
            CreatedAt = DateTime.UtcNow
        };

        // Send to hub as pending action for human decision
        await _hubConnection.SendPendingActionAsync(action);
        _logger.LogInformation($"Created pending action for PR {pr.Number} comment");
    }
}
```

#### Configuration (`config.yaml` PR Monitoring Section)

```yaml
pr_monitoring:
  enabled: true
  polling_interval_minutes: 15
  confidence_threshold: 0.8          # Min confidence to auto-act; lower = escalate to user
  monitored_repositories:
    - path: "/home/user/repos/project-alpha"
      enabled: true
    - path: "/home/user/repos/api-service"
      enabled: true
  auto_fix_enabled: true             # Create fix jobs for actionable comments
  respond_to_invalid: true           # Respond to misunderstandings
  escalate_ambiguous: true           # Create pending actions for unclear comments
```

#### Example Flow

1. **Polling Interval:** Every 15 minutes, spoke fetches open PRs from monitored repos.
2. **Comment Detection:** For each PR, spoke fetches unprocessed review comments.
3. **Classification:** Each comment is classified via Claude API (example: "Add null check" → Actionable, high confidence).
4. **Action:**
   - **Actionable + High Confidence** → Spoke creates a fix job, worker implements the fix, spoke pushes commit and responds to comment.
   - **Invalid + High Confidence** → Spoke responds explaining why the current approach is correct.
   - **Positive** → No action, marked as processed.
   - **Ambiguous or Low Confidence** → Creates PendingAction (PrReview gate) in user's Awaiting Input queue.

#### Credentials & Security

- Git platform credentials (GitHub token, Azure DevOps PAT) are stored locally on the spoke in `config.yaml` or environment variables.
- The spoke uses credentials locally to interact with PR APIs. Credentials **never** flow to the hub.
- The hub is notified only of the classification result, pending actions, and job creation — not the credentials or detailed PR content.

## 5. Worker Container Design

The worker container is a Docker image containing Claude Code CLI, configured to execute a specific job. Workers have access to both spoke-level and project-level skills.

#### Dockerfile

```dockerfile
FROM ubuntu:22.04

# Install dependencies
RUN apt-get update && apt-get install -y \
    curl \
    git \
    build-essential \
    python3 \
    python3-pip \
    jq \
    && rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI
RUN curl -fsSL https://github.com/anthropics/claude-code/releases/download/latest/claude-code-linux.sh \
    | bash

# Create workspace directory
RUN mkdir -p /workspace/{repo,output,skills}
WORKDIR /workspace

# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
CMD ["claude", "code", "/workspace/prompt.md"]
```

#### Skills Mounting

When the spoke launches a worker container, it mounts both spoke-level and project-level skills:

```csharp
// In SpokeDockerService.ExecuteJobAsync()

var mounts = new List<Mount>
{
    new Mount
    {
        Type = "bind",
        Source = repoPath,
        Target = "/workspace/repo",
        ReadOnly = false
    },
    new Mount
    {
        Type = "bind",
        Source = promptFile,
        Target = "/workspace/prompt.md",
        ReadOnly = true
    },
    new Mount
    {
        Type = "bind",
        Source = outputDirectory,
        Target = "/workspace/output",
        ReadOnly = false
    }
};

// Mount spoke-level skills
var spokeSkillsPath = Path.Join(workspacePath, ".nexus", "skills");
if (Directory.Exists(spokeSkillsPath))
{
    mounts.Add(new Mount
    {
        Type = "bind",
        Source = spokeSkillsPath,
        Target = "/workspace/skills/spoke",
        ReadOnly = true
    });
}

// Mount project-level skills (override spoke)
var projectSkillsPath = Path.Join(workspacePath, "projects", projectKey, ".nexus", "skills");
if (Directory.Exists(projectSkillsPath))
{
    mounts.Add(new Mount
    {
        Type = "bind",
        Source = projectSkillsPath,
        Target = "/workspace/skills/project",
        ReadOnly = true
    });
}

// Create container with all mounts
var createResponse = await _dockerClient.Containers.CreateContainerAsync(
    new CreateContainerParameters
    {
        Image = _workerImage,
        Name = containerName,
        HostConfig = new HostConfig
        {
            Mounts = mounts.ToList()
        }
    },
    cancellationToken);
```

#### entrypoint.sh

```bash
#!/bin/bash

set -e

# Log environment
echo "=== Worker Container Starting ==="
echo "Workspace: $(pwd)"
echo "Repo path: /workspace/repo"
echo "Prompt file: /workspace/prompt.md"
echo "Output directory: /workspace/output"
echo "Skills path: /workspace/skills"

# Validate mounted volumes
if [ ! -f /workspace/prompt.md ]; then
    echo "ERROR: Prompt file not found at /workspace/prompt.md"
    exit 1
fi

if [ ! -d /workspace/repo ]; then
    echo "ERROR: Repo directory not found at /workspace/repo"
    exit 1
fi

# Build skills path argument
# Project skills override spoke skills, so put project first
SKILLS_PATHS=""
if [ -d /workspace/skills/project ]; then
    SKILLS_PATHS="/workspace/skills/project"
fi
if [ -d /workspace/skills/spoke ]; then
    if [ -z "$SKILLS_PATHS" ]; then
        SKILLS_PATHS="/workspace/skills/spoke"
    else
        SKILLS_PATHS="$SKILLS_PATHS:/workspace/skills/spoke"
    fi
fi

# Execute Claude Code with skills
echo "=== Executing Claude Code ==="
if [ -z "$SKILLS_PATHS" ]; then
    exec claude code /workspace/prompt.md
else
    exec claude code --skills-path "$SKILLS_PATHS" /workspace/prompt.md
fi
```

#### Input/Output Contract

**Mounted Volumes:**
- `/workspace/repo` (read-write) — Clone of the Git repository. Worker commits/pushes from here.
- `/workspace/prompt.md` (read-only) — Full assembled prompt for the worker.
- `/workspace/output` (read-write) — Directory where worker writes artifacts (diffs, logs, summaries, etc.).
- `/workspace/skills/spoke` (read-only) — Spoke-level skills (if present).
- `/workspace/skills/project` (read-only) — Project-level skills (if present, overrides spoke).

**Stdout/Stderr:**
- All output from Claude Code streams through stdout/stderr.
- Spoke captures in real-time and sends to hub via SignalR.

**Exit Code:**
- `0` — Success
- Non-zero — Failure. Hub UI displays failure with logs.

---

## 6. Spoke Configuration & Skills System

### 6.1 Spoke Configuration (config.yaml)

The spoke daemon reads configuration from an OS-appropriate location:
- **Linux/macOS:** `~/.nexus/config.yaml` (resolves via `$HOME`)
- **Windows:** `%LOCALAPPDATA%\Nexus\config.yaml` (resolves via `%LOCALAPPDATA%`)

```yaml
# ~/.nexus/config.yaml (or %LOCALAPPDATA%\Nexus\config.yaml on Windows)

spoke:
  id: "spoke-alpha-001"
  name: "Work Laptop"
  ccSessionId: "cc-session-uuid-here"  # CC session ID for spoke agent
  # Reported to hub to identify the OS this spoke runs on
  os: "windows"  # "windows", "macos", or "linux"
  architecture: "x64"  # "x64" or "arm64"

hub:
  url: "wss://nexus.tailnet.com/api/hub"
  token: "${HUB_SPOKE_TOKEN}"  # From environment variable

capabilities:
  jira: true
  git: true
  docker: true
  pr_monitoring: true

jira:
  instances:
    - name: "company"
      url: "https://company.atlassian.net"
      username: "${JIRA_USERNAME}"
      apiToken: "${JIRA_API_TOKEN}"

git:
  defaultBranch: "main"
  pushBranches: true
  autoCommit: true

docker:
  # Note: Docker Desktop on Windows/macOS, Docker Engine on Linux
  # All worker containers run Linux regardless of host OS
  resourceLimits:
    memoryBytes: 2147483648  # 2GB
    cpus: 2

prMonitoring:
  enabled: true
  interval: 15  # minutes
  platforms:
    - github:
        token: "${GITHUB_TOKEN}"
        org: "your-org"
    - azure:
        token: "${AZURE_DEVOPS_TOKEN}"
        org: "your-org"

workspace:
  # Resolved at runtime using Environment.GetFolderPath()
  baseDirectory: "${HOME}/.nexus"  # Auto-resolves to OS-appropriate path
  skillsDirectory: "${HOME}/.nexus/skills"

logging:
  level: "Information"
  filePath: "${HOME}/.nexus/logs/spoke.log"
```

### 6.2 Skills Directory Structure

Spoke-level skills live in `~/.nexus/skills/` and are merged with project-level skills at runtime.

```
~/.nexus/skills/
├── CLAUDE.md                    # Spoke agent behavior and instructions
├── conventions/
│   ├── coding-style.md          # Code conventions for this machine
│   ├── test-guidelines.md       # Testing patterns
│   ├── git-workflow.md          # Git branching and commit strategy
│   └── pr-template.md           # PR description template
├── environment/
│   ├── setup-notes.md           # Environment-specific setup
│   ├── credentials.md           # Credential management (non-sensitive examples)
│   └── troubleshooting.md       # Common issues and fixes
├── templates/
│   ├── implementation-plan.md   # Template for implementation plans
│   └── job-summary.md           # Template for job summaries
└── reference/
    ├── frequently-used.md       # Frequently needed patterns
    └── gotchas.md               # Common pitfalls
```

**Example CLAUDE.md (Spoke Level):**
```markdown
# Nexus Spoke Agent

You are the agent for the "Work Laptop" spoke. Your role is to:
- Manage projects and coordinate work
- Respond to queries about local state
- Plan and execute coding tasks
- Monitor pull requests and respond to feedback
- Maintain institutional knowledge

## Coding Conventions
- Use 2-space indentation for JavaScript/TypeScript, 4-space for Python
- Write tests for all new features
- Follow the architectural patterns in ../reference/architecture.md

## When to Create a Job
- If a task requires code changes, create a job
- If it's just information gathering or planning, respond directly
- Escalate ambiguous decisions to the user

## Memory Management
- After each job, update memories/ with learnings
- Keep decision-log.md updated with key decisions
- Summarize codebase patterns in codebase-notes.md
```

### 6.3 Project-Level Skills

Project-level skills override spoke-level skills for a specific project.

```
~/.nexus/projects/PROJ-4521/.nexus/skills/
├── CLAUDE.md                    # Project-specific instructions
├── architecture/
│   ├── api-patterns.md          # API conventions for this project
│   ├── db-schema.md             # Database patterns
│   └── service-boundaries.md    # Service architecture
├── gotchas.md                   # Project-specific pitfalls
└── reference/
    ├── deployment.md            # Deployment process
    └── monitoring.md            # Monitoring and alerts
```

**Example CLAUDE.md (Project Level):**
```markdown
# PROJ-4521: MyApp Backend

You are working on the MyApp backend project.

## Architecture
This is a .NET 7 service with Entity Framework Core and PostgreSQL.
Key services:
- AuthService: JWT token generation and validation
- UserService: User profile and preferences
- NotificationService: Email/SMS notifications (via SendGrid)

## Before Making Changes
1. Check the current architecture in architecture/service-boundaries.md
2. Ensure changes align with API-patterns.md
3. Run tests locally before committing

## Known Issues
- PostgreSQL migrations must include rollback scripts (see gotchas.md)
- The AuthService is being refactored in parallel; coordinate with the auth branch

## Deployment
Always run ./scripts/pre-deploy-checks.sh before pushing
```

### 6.4 Skills Merge Logic

When invoking a CC instance (spoke agent or worker), skills are merged:

```
spoke-level skills
    ↓ (project-level skills override)
project-level skills
    ↓ (passed to CC with --skills-path)
CC instance
```

The merge is handled by passing both paths to CC: `--skills-path /project/skills:/spoke/skills`

CC loads skills in order; later paths override earlier ones.

---

## 7. Deployment Architecture

### 7.1 Hub Deployment (Self-Hosted Kubernetes)

Hub runs on a Kubernetes cluster (lightweight k3s) on self-hosted infrastructure. Single-node cluster for now.

#### k3s Deployment Manifests

**Namespace:**
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: nexus
```

**PostgreSQL StatefulSet:**
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: nexus
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 50Gi

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: nexus
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:16
        ports:
        - containerPort: 5432
        env:
        - name: POSTGRES_DB
          value: nexus
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: postgres-secret
              key: password
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
```

**Hub API Deployment:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nexus-hub-api
  namespace: nexus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: nexus-hub-api
  template:
    metadata:
      labels:
        app: nexus-hub-api
    spec:
      containers:
      - name: api
        image: nexus-hub-api:latest
        imagePullPolicy: Never  # Using local k3s image
        ports:
        - containerPort: 5000
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: hub-secret
              key: db-connection-string
        - name: Google__ClientId
          valueFrom:
            secretKeyRef:
              name: hub-secret
              key: google-client-id
        - name: Google__ClientSecret
          valueFrom:
            secretKeyRef:
              name: hub-secret
              key: google-client-secret
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 10
```

**Hub UI Deployment:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nexus-hub-ui
  namespace: nexus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: nexus-hub-ui
  template:
    metadata:
      labels:
        app: nexus-hub-ui
    spec:
      containers:
      - name: ui
        image: nexus-hub-ui:latest
        imagePullPolicy: Never
        ports:
        - containerPort: 3000
        env:
        - name: NEXT_PUBLIC_API_URL
          value: "https://nexus.tailnet.com/api"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

**Services:**
```yaml
apiVersion: v1
kind: Service
metadata:
  name: nexus-hub-api
  namespace: nexus
spec:
  selector:
    app: nexus-hub-api
  ports:
  - protocol: TCP
    port: 5000
    targetPort: 5000
  type: ClusterIP

---
apiVersion: v1
kind: Service
metadata:
  name: nexus-hub-ui
  namespace: nexus
spec:
  selector:
    app: nexus-hub-ui
  ports:
  - protocol: TCP
    port: 3000
    targetPort: 3000
  type: ClusterIP

---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: nexus
spec:
  selector:
    app: postgres
  ports:
  - protocol: TCP
    port: 5432
    targetPort: 5432
  type: ClusterIP
```

**Ingress (Tailscale + TLS):**
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: nexus-ingress
  namespace: nexus
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: traefik
  tls:
  - hosts:
    - nexus.tailnet.com
    secretName: nexus-tls
  rules:
  - host: nexus.tailnet.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: nexus-hub-api
            port:
              number: 5000
      - path: /
        pathType: Prefix
        backend:
          service:
            name: nexus-hub-ui
            port:
              number: 3000
```

### 7.2 Spoke Deployment

Spoke runs as a native service on each platform: systemd service on Linux, Windows Service on Windows, launchd on macOS.

#### Linux: systemd Service File

```ini
# /etc/systemd/system/nexus-spoke.service

[Unit]
Description=Nexus Spoke Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=username
WorkingDirectory=/home/user/.nexus
ExecStart=/usr/local/bin/nexus-spoke
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

#### Windows: Windows Service (using .NET BackgroundService + WindowsServiceLifetime)

Spoke .NET project configured with `Microsoft.Extensions.Hosting.WindowsServices`. Installation:

```powershell
# Build self-contained executable
dotnet publish -c Release -r win-x64 --self-contained

# Install as service
sc.exe create NexusSpokeAgent binPath= "C:\Program Files\NexusSpokeAgent\nexus-spoke.exe"
sc.exe start NexusSpokeAgent

# Or use Windows Service Installer (wix-based) in future
```

#### macOS: launchd plist

```xml
<!-- ~/.config/launchd/user/com.nexus.spoke.plist -->

<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.nexus.spoke</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/nexus-spoke</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/Users/user/.nexus</string>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/var/log/nexus-spoke.log</string>
    <key>StandardErrorPath</key>
    <string>/var/log/nexus-spoke.log</string>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
```

#### Cross-Platform Installation: Self-Contained Executables

Alternatively, publish platform-specific self-contained binaries via GitHub Releases:

```bash
# Build for each platform
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/win-x64
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o dist/osx-arm64
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o dist/linux-x64

# Publish to GitHub Releases
# Assets: nexus-spoke-win-x64.exe, nexus-spoke-osx-arm64, nexus-spoke-linux-x64
```

#### Installation Script (Cross-Platform)

```bash
#!/bin/bash
# install-spoke.sh — cross-platform installation script

set -e

# Detect OS
OS=$(uname -s)
ARCH=$(uname -m)

case "$OS" in
    Linux)
        if [ "$ARCH" = "x86_64" ]; then
            PLATFORM="linux-x64"
        else
            echo "Unsupported architecture: $ARCH"
            exit 1
        fi
        INSTALL_DIR="/usr/local/bin"
        WORKSPACE_DIR="$HOME/.nexus"
        SERVICE_MANAGER="systemd"
        ;;
    Darwin)
        if [ "$ARCH" = "arm64" ]; then
            PLATFORM="osx-arm64"
        elif [ "$ARCH" = "x86_64" ]; then
            PLATFORM="osx-x64"
        else
            echo "Unsupported architecture: $ARCH"
            exit 1
        fi
        INSTALL_DIR="/usr/local/bin"
        WORKSPACE_DIR="$HOME/.nexus"
        SERVICE_MANAGER="launchd"
        ;;
    MINGW64_NT*|MSYS_NT*)
        # Windows (Git Bash)
        PLATFORM="win-x64"
        INSTALL_DIR="C:\\Program Files\\NexusSpokeAgent"
        WORKSPACE_DIR="$LOCALAPPDATA/Nexus"
        SERVICE_MANAGER="windows-service"
        ;;
    *)
        echo "Unsupported OS: $OS"
        exit 1
        ;;
esac

echo "Installing Nexus Spoke for $OS ($ARCH)"

# Download latest release
RELEASE_URL="https://github.com/eaglebyte/nexus/releases/latest/download/nexus-spoke-${PLATFORM}"
echo "Downloading from $RELEASE_URL"
curl -fsSL $RELEASE_URL -o nexus-spoke
chmod +x nexus-spoke

# Install to system directory
sudo mv nexus-spoke $INSTALL_DIR/nexus-spoke || mv nexus-spoke $INSTALL_DIR/nexus-spoke

# Create workspace directory
mkdir -p "$WORKSPACE_DIR/.nexus/{memories,agent-state}"
mkdir -p "$WORKSPACE_DIR/{projects,templates,logs}"

# Copy config template
if [ -f "config.yaml.example" ]; then
    cp config.yaml.example "$WORKSPACE_DIR/.nexus/config.yaml"
    echo "Created config at $WORKSPACE_DIR/.nexus/config.yaml"
    echo "⚠️  Edit config.yaml with your hub URL and authentication token"
fi

# Install service based on OS
case "$SERVICE_MANAGER" in
    systemd)
        echo "Installing systemd service..."
        sudo tee /etc/systemd/system/nexus-spoke.service > /dev/null << EOF
[Unit]
Description=Nexus Spoke Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$WORKSPACE_DIR
ExecStart=$INSTALL_DIR/nexus-spoke
Restart=always
RestartSec=10
Environment="ASPNETCORE_ENVIRONMENT=Production"
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF
        sudo systemctl daemon-reload
        sudo systemctl enable nexus-spoke
        sudo systemctl start nexus-spoke
        echo "✓ Systemd service installed and started"
        systemctl status nexus-spoke
        ;;
    launchd)
        echo "Installing launchd plist..."
        mkdir -p "$HOME/.config/launchd/user"
        cat > "$HOME/.config/launchd/user/com.nexus.spoke.plist" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.nexus.spoke</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/nexus-spoke</string>
    </array>
    <key>WorkingDirectory</key>
    <string>$HOME/.nexus</string>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/var/log/nexus-spoke.log</string>
    <key>StandardErrorPath</key>
    <string>/var/log/nexus-spoke.log</string>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
EOF
        launchctl load "$HOME/.config/launchd/user/com.nexus.spoke.plist"
        echo "✓ launchd service installed and started"
        launchctl list | grep nexus.spoke
        ;;
    windows-service)
        echo "Installing Windows Service..."
        echo "Run the following as Administrator:"
        echo "  sc.exe create NexusSpokeAgent binPath= \"$INSTALL_DIR\\nexus-spoke.exe\""
        echo "  sc.exe start NexusSpokeAgent"
        ;;
esac

echo ""
echo "✓ Nexus Spoke installation complete!"
echo "Workspace: $WORKSPACE_DIR"
echo "Executable: $INSTALL_DIR/nexus-spoke"
```

### 7.3 Networking

**Tailscale Integration:**

Both hub and spokes are on a Tailscale network. Spoke initiates outbound connection to hub's Tailscale IP.

- Hub: Accessible via `https://nexus.tailnet.com` (Tailscale domain).
- Spoke config includes hub URL: `https://nexus.tailnet.com/api/hub`.
- All communication encrypted via Tailscale WireGuard and TLS.

**Alternative: Cloudflare Tunnel**

If public internet access preferred:
- Hub exposed via Cloudflare Tunnel.
- Spoke connects to `https://nexus.example.com/api/hub`.

### 7.4 Docker Prerequisite (Worker Containers)

**Critical:** Spokes require Docker to be available for worker container execution. Worker containers are always Linux containers regardless of the host OS.

**Linux:**
- Docker Engine (native). Installation: `apt-get install docker-ce` or per your distro
- Daemon running and accessible: `docker ps`

**Windows:**
- Docker Desktop (with WSL2 backend recommended) OR Docker Engine in WSL2
- Installation: https://docs.docker.com/desktop/install/windows-install/
- Verify: `docker ps` from PowerShell or Git Bash

**macOS:**
- Docker Desktop or OrbStack
- Installation: https://docs.docker.com/desktop/install/mac-install/
- Verify: `docker ps` from terminal

**Note on Worker Containers:**
Workers are published as Linux containers (e.g., Ubuntu 24.04 base). Docker Desktop on Windows/macOS handles the Linux VM layer transparently. Spoke code does not need to be Linux; only workers are containerized Linux.

---

## 8. Repository Structure

### Monorepo Layout

```
nexus/
├── .github/
│   └── workflows/
│       ├── build-and-test.yml        # CI/CD pipeline
│       └── release.yml
│
├── hub/
│   ├── Nexus.Hub.sln
│   ├── src/
│   │   ├── Nexus.Hub.Api/
│   │   │   ├── Nexus.Hub.Api.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/
│   │   │   ├── Hubs/
│   │   │   ├── Services/
│   │   │   ├── Models/
│   │   │   ├── Middleware/
│   │   │   └── appsettings.json
│   │   ├── Nexus.Hub.Domain/
│   │   │   ├── Nexus.Hub.Domain.csproj
│   │   │   ├── Entities/
│   │   │   ├── Events/
│   │   │   └── Repositories/
│   │   ├── Nexus.Hub.Infrastructure/
│   │   │   ├── Nexus.Hub.Infrastructure.csproj
│   │   │   ├── Data/
│   │   │   ├── Repositories/
│   │   │   ├── Services/
│   │   │   └── Authentication/
│   │   └── Nexus.Hub.Tests/
│   │       └── (xUnit tests)
│   ├── web/
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── next.config.js
│   │   ├── app/
│   │   ├── components/
│   │   ├── lib/
│   │   ├── styles/
│   │   └── public/
│   ├── Dockerfile.api
│   ├── Dockerfile.ui
│   └── docker-compose.dev.yml
│
├── spoke/
│   ├── Nexus.Spoke.sln
│   ├── src/
│   │   ├── Nexus.Spoke/
│   │   │   ├── Nexus.Spoke.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Services/
│   │   │   ├── Models/
│   │   │   ├── Handlers/
│   │   │   ├── Workers/
│   │   │   ├── config.yaml
│   │   │   └── appsettings.json
│   │   └── Nexus.Spoke.Tests/
│   ├── Dockerfile
│   └── install.sh
│
├── worker/
│   ├── Dockerfile                    # Claude Code worker image
│   ├── entrypoint.sh
│   └── README.md
│
├── k8s/
│   ├── namespace.yaml
│   ├── postgres.yaml
│   ├── hub-api.yaml
│   ├── hub-ui.yaml
│   ├── services.yaml
│   └── ingress.yaml
│
├── docs/
│   ├── technical-design.md           # This file
│   ├── architecture.md
│   ├── getting-started.md
│   ├── spoke-setup.md
│   ├── hub-deployment.md
│   ├── api-reference.md
│   └── contributing.md
│
├── scripts/
│   ├── build-hub.sh
│   ├── build-spoke.sh
│   ├── build-worker.sh
│   ├── deploy-hub.sh
│   └── local-dev.sh
│
├── LICENSE
├── README.md
└── .gitignore
```

### Solution Structure (Visual Studio)

**Nexus.Hub.sln:**
- Nexus.Hub.Api
- Nexus.Hub.Domain
- Nexus.Hub.Infrastructure
- Nexus.Hub.Tests

**Nexus.Spoke.sln:**
- Nexus.Spoke
- Nexus.Spoke.Tests

---

## 9. Development Workflow

### Local Hub Setup

```bash
# Prerequisites: .NET 10 SDK, Node.js 18+, Docker, PostgreSQL

# Clone repo
git clone https://github.com/eaglebyte/nexus.git
cd nexus/hub

# Start PostgreSQL (Docker)
docker run -d \
  --name nexus-postgres \
  -e POSTGRES_DB=nexus \
  -e POSTGRES_USER=nexus \
  -e POSTGRES_PASSWORD=dev \
  -p 5432:5432 \
  postgres:16

# Update appsettings.Development.json
cat > src/Nexus.Hub.Api/appsettings.Development.json << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nexus;Username=nexus;Password=dev"
  },
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  }
}
EOF

# Run migrations
cd src/Nexus.Hub.Api
dotnet ef database update -p ../Nexus.Hub.Infrastructure

# Start API (runs on http://localhost:5000)
dotnet run

# In another terminal, start UI
cd web
npm install
npm run dev  # Runs on http://localhost:3000
```

### Local Spoke Setup

```bash
# On a different machine (or same machine, different port for testing)

# Clone repo
git clone https://github.com/eaglebyte/nexus.git
cd nexus/spoke

# Build
dotnet build

# Configure
mkdir -p ~/.nexus-spoke/.nexus/{memories,agent-state}
cat > ~/.nexus-spoke/.nexus/config.yaml << EOF
spoke:
  id: "00000000-0000-0000-0000-000000000001"
  name: "Local Dev Spoke"
  capabilities: ["Jira", "Git", "Docker"]

hub:
  url: "http://localhost:5000/api/hub"
  token: "dev-token-12345"

workspace:
  basePath: "~/.nexus-spoke"

jira:
  enabled: false

git:
  enabled: true

docker:
  enabled: true
  workerImage: "nexus/claude-code-worker:latest"

approval:
  mode: "plan_review"

logging:
  level: "Debug"
EOF

# Run
dotnet run --project src/Nexus.Spoke
```

### Testing Strategy

#### Unit Tests (xUnit + Moq)

```csharp
// Tests/JobServiceTests.cs

[Fact]
public async Task CreateJobAsync_ValidRequest_ReturnsJobWithQueuedStatus()
{
    // Arrange
    var mockRepo = new Mock<IJobRepository>();
    var service = new JobService(mockRepo.Object, _logger);
    var request = new CreateJobRequest { ProjectId = Guid.NewGuid(), Type = "Implement" };

    // Act
    var job = await service.CreateJobAsync(request);

    // Assert
    Assert.NotNull(job);
    Assert.Equal(JobStatus.Queued, job.Status);
    mockRepo.Verify(r => r.AddAsync(It.IsAny<Job>()), Times.Once);
}
```

#### Integration Tests

```csharp
// Tests/HubIntegrationTests.cs

[Fact]
public async Task SpokeRegistration_ValidToken_SpokeBecomesOnline()
{
    // Use WebApplicationFactory to spin up test server
    using var client = _factory.CreateClient();

    var payload = new SpokeRegistrationPayload { ... };
    var response = await client.PostAsJsonAsync("/api/spokes/register", payload);

    Assert.True(response.IsSuccessStatusCode);
    // Verify spoke is in database with status Online
}
```

#### E2E Tests (Selenium or Playwright)

```typescript
// E2E spec: User can view spoke status on dashboard

test("Dashboard displays connected spokes", async ({ page }) => {
  await page.goto("http://localhost:3000/dashboard");
  await page.fill('input[name="email"]', "test@example.com");
  await page.click("text=Sign in with Google");
  // OAuth flow...

  const spokeCard = page.locator("text=Work Laptop");
  await expect(spokeCard).toBeVisible();
  await expect(page.locator("text=Online")).toBeVisible();
});
```

---

## Implementation Priorities

**Phase 1 (Weeks 1-2):**
- Hub API skeleton (registration, heartbeat, basic REST endpoints).
- SignalR hub with connection lifecycle.
- PostgreSQL schema and EF Core setup.
- Spoke daemon with outbound WebSocket connection.

**Phase 2 (Weeks 3-4):**
- Next.js UI with dashboard (spoke list, status).
- Workspace and project creation (spoke-side).
- Jira integration (pull ticket details).

**Phase 3 (Weeks 5-6):**
- Docker integration (launch worker containers).
- Terminal output streaming hub ↔ spoke.
- Job creation and approval flow.

**Phase 4 (Weeks 7+):**
- Memory system (read/write/summarize).
- Plan generation and review.
- Autonomous job execution ("work through backlog").
- UI refinement, error handling, production hardening.

---

## Deployment Checklist

- [ ] Hub API and UI Dockerfiles tested locally
- [ ] Kubernetes cluster provisioned on self-hosted infrastructure
- [ ] PostgreSQL backed by persistent volume
- [ ] Hub deployed to k3s with correct environment variables
- [ ] Tailscale network configured; hub accessible at `nexus.tailnet.com`
- [ ] Spoke service file created and enabled on spoke machine
- [ ] Spoke config points to hub URL with valid token
- [ ] End-to-end test: spoke connects, hub displays online status
- [ ] Google OAuth configured; user can sign in
- [ ] Logs aggregated (journalctl for spoke, k3s logs for hub)
- [ ] Backups scheduled for PostgreSQL volume

---

**Document Version:** 1.0
**Last Updated:** 2026-04-04
**Author:** Claude Code
