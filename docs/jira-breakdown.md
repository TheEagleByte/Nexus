# Nexus — Jira Epic Breakdown

**Project Key:** NEX (new)
**Framework:** AI-Accelerated Development (Claude Code)
**Timeline:** ~4-6 weeks
**Last Updated:** 2026-04-04

---

## Summary

| Metric | Value |
|--------|-------|
| **Total Epics** | 25 |
| **Total Stories** | 129 |
| **Total Story Points** | 313 |
| **Phase 1 (Foundation — MVP, No Auth)** | 37 stories, 78 pts (~1 week) |
| **Phase 2 (Workspace + Skills + Conversations)** | 25 stories, 54 pts (~1 week) |
| **Phase 3 (Workers)** | 17 stories, 50 pts (~1 week) |
| **Phase 4 (Intelligence + Hub Meta-Agent)** | 35 stories, 91 pts (~1-2 weeks) |
| **Phase 5 (Polish & MVP Launch)** | 8 stories, 19 pts (~1 week) |
| **Phase 6 (Post-MVP: Auth & Remote Access)** | 7 stories, 21 pts (not included in MVP) |

---

## Project Setup

**Jira Instance:** eaglebyte.atlassian.net
**Suggested Key:** NEX
**Type:** Team-managed software project

**Issue Types:**
- Epic (containers for themes)
- Story (user-facing work with acceptance criteria)
- Task (supporting work, no user story)
- Bug (defects found during development)

## Unit Test Expectations

All stories include unit tests as part of development. Tests are not separate stories. Every implementation story's acceptance criteria implicitly includes:
- Unit tests created alongside implementation
- Tests achieve >80% code coverage for new code
- All tests pass before story is marked done
- Tests remain maintainable and clearly document expected behavior

---

**Custom Fields (recommended):**
- Story Points (dropdown: 1, 2, 3, 5, 8)
- Acceptance Criteria (text area)
- Phase (dropdown: 1, 2, 3, 4, 5)

**Labels:**
- `hub-api`, `hub-ui`, `spoke`, `worker`, `database`
- `critical-path`, `spike`, `investigation`

---

## Phase 1 — Foundation: MVP (No Authentication) (~1 week, 82 points)

Foundation establishes the core communication backbone: Hub API with database, SignalR infrastructure, Spoke connection, and basic UI. **MVP assumes trusted LAN — no authentication required.** By end of Phase 1, a spoke can register with the hub and maintain a persistent connection. Hub accessible via local IP on LAN (e.g., `http://192.168.1.100:5000`).

**Key constraint for MVP:** No Google OAuth, no TLS, no authentication middleware. Add auth in Phase 6 (post-MVP).

### Epic: Hub API Initialization

**Description:**
Set up the .NET 10 Hub API project structure with DI, middleware, and PostgreSQL integration. Create core service interfaces and repository abstractions. This epic establishes the foundation for all subsequent hub-side development.

**Total Points:** 16
**Target Week:** Week 1

#### Stories

**NEX-1: Initialize .NET 10 solution structure**
Create Nexus.Hub.sln with four projects: Nexus.Hub.Api (ASP.NET Core), Nexus.Hub.Domain (interfaces/entities), Nexus.Hub.Infrastructure (EF Core, concrete services), Nexus.Hub.Tests (xUnit). Set up global.json for .NET 10, establish project references, add standard NuGet dependencies (EF Core, SignalR, Serilog).

- [ ] Nexus.Hub.sln created with four projects in correct structure
- [ ] Program.cs skeleton with basic middleware pipeline
- [ ] All projects reference each other correctly
- [ ] Solution builds without errors

**Points:** 2

---

**NEX-2: Set up PostgreSQL with EF Core and initial migration**
Configure DbContext for Spoke, Project, Job, Message, OutputStream, User, and AuditLog entities. Create EF Core migrations for schema initialization. Set up design-time factory for migrations. Database should be fully normalized with appropriate indexes and foreign keys.

- [ ] DbContext with all seven entity sets defined
- [ ] Entity models with correct data types and constraints
- [ ] Initial migration created and tested locally
- [ ] Indexes on frequently-queried columns (spoke status, project key, job status)

**Points:** 3

---

**NEX-3: Implement core service interfaces and repositories**
Define service interfaces (ISpokeService, IProjectService, IJobService, IMessageService) with method signatures. Create repository interfaces (ISpokeRepository, IProjectRepository, IJobRepository). Flesh out DTOs and request/response models. Lay the foundation for all CRUD operations.

- [ ] All four service interfaces defined with appropriate async methods
- [ ] Repository interfaces defined
- [ ] Request/response DTOs created for all major operations
- [ ] No implementation logic yet—interfaces only

**Points:** 3

---

**NEX-4: Set up dependency injection and middleware**
Configure DI in Program.cs: add DbContext, services, repositories, SignalR, CORS, logging (Serilog). Add exception handling middleware, request logging middleware, and authentication middleware stubs (Google OAuth configured but not implemented yet).

- [ ] All services and repositories registered in DI
- [ ] CORS policy configured to allow localhost:3000
- [ ] Exception middleware handles and logs errors
- [ ] Request logging captures all API calls

**Points:** 2

---

**NEX-5: Implement spoke registration endpoint (POST /api/spokes/register)**
Create SpokeController with RegisterAsync handler. Accept SpokeRegistrationRequest (id, name, capabilities, config). Validate token (pre-shared token or certificate). Store spoke in database. Return 201 Created with spoke details. Include basic validation and error handling.

- [ ] Endpoint accepts and validates registration payload
- [ ] Token validation works (accepts hardcoded dev token for now)
- [ ] Spoke persisted to database with OnCreated timestamp
- [ ] Returns 201 with spoke entity in response

**Points:** 2

---

**NEX-6: Implement spoke status and metadata retrieval endpoints**
Create handlers for GET /api/spokes (list all), GET /api/spokes/{id} (get single). Include filtering by status (online/offline). Return comprehensive spoke metadata including last_seen, capabilities, config.

- [ ] GET /api/spokes returns list of all spokes with pagination
- [ ] GET /api/spokes/{id} returns full spoke details
- [ ] Spoke status reflects current online/offline state
- [ ] Endpoints are fast (< 100ms even with many spokes)

**Points:** 2

---

### Epic: SignalR Hub & Spoke Connection

**Description:**
Implement the SignalR hub for real-time bidirectional communication between hub and spokes. Establish connection lifecycle management, group-based broadcasting, reconnection logic, and heartbeat monitoring. This is the nervous system of the platform.

**Total Points:** 15
**Target Week:** Week 1

#### Stories

**NEX-7: Create SignalR hub class with connection lifecycle**
Implement NexusHub : Hub with OnConnectedAsync, OnDisconnectedAsync handlers. Set up ConnectionToSpokeMap (static dict) to track connection IDs to spoke IDs. Add spoke to appropriate group on connection. Update spoke status to Online. On disconnect, update status to Offline.

- [ ] NexusHub.cs implements all Hub lifecycle methods
- [ ] Spokes correctly added to `spoke-{spokeId}` groups
- [ ] Status updates on connection/disconnection happen reliably
- [ ] No memory leaks from connection tracking

**Points:** 3

---

**NEX-8: Implement spoke registration and heartbeat hub methods**
Add SpokeRegisterAsync and SpokeHeartbeatAsync methods to NexusHub. Spoke calls SpokeRegisterAsync on connection with registration payload. Hub updates spoke status and logs event. SpokeHeartbeatAsync called every 30 seconds; hub updates LastSeen timestamp and schedules timeout check (90 seconds = 3 missed heartbeats).

- [ ] SpokeRegisterAsync stores connection → spoke mapping
- [ ] Heartbeat updates LastSeen correctly
- [ ] Timeout logic marks spoke as Offline after 3 missed heartbeats
- [ ] Events logged with correlation IDs for debugging

**Points:** 3

---

**NEX-9: Implement hub → spoke job assignment method**
Add AssignJobAsync(spokeId, jobPayload) to NexusHub. Hub broadcasts job assignment to specific spoke via group. Spoke receives on client side (JavaScript). Hub logs assignment for audit. Support for optional approval gate: if ApprovalRequired = true, hub sends message asking spoke to generate plan; if false, hub immediately tells spoke to start.

- [ ] AssignJobAsync sends to correct spoke group
- [ ] Payload includes full job details and project context
- [ ] Logged for audit trail
- [ ] Supports both immediate execution and plan-review modes

**Points:** 2

---

**NEX-10: Implement spoke → hub status update methods**
Add ProjectStatusChangedAsync, JobStatusChangedAsync, and JobOutputAsync to NexusHub. Spoke sends status updates; hub persists to database and broadcasts to all clients for real-time dashboard update. Job output streamed in chunks; hub appends to OutputStream table.

- [ ] Status updates persisted before broadcast
- [ ] Output streamed reliably without loss
- [ ] Timestamps captured consistently
- [ ] Broadcast reaches all connected UI clients

**Points:** 2

---

**NEX-11: Implement conversational message relay**
Add MessageFromSpokeAsync and SendMessageToSpokeAsync to NexusHub. User can send conversational message from hub UI to spoke. Spoke responds; message streams back. All messages logged to Messages table with timestamp and optional job context. Support for both sync and async message handling.

- [ ] Messages persisted with direction (to_spoke, from_spoke)
- [ ] Message history retrievable via GET /api/spokes/{id}/conversation
- [ ] Handles out-of-order or concurrent messages gracefully
- [ ] Supports long messages (multi-line, large context dumps)

**Points:** 2

---

**NEX-12: Add spoke group management and reconnection logic**
Spokes added to groups on connection. On reconnection after temporary disconnect, spoke re-authenticates and re-joins group. Queued jobs/messages sent to spoke on reconnection. Implement backoff for reconnection attempts (exponential, max 5 minutes).

- [ ] Re-joining group after network loss works
- [ ] Queued jobs replayed on reconnection
- [ ] Exponential backoff prevents hammering hub
- [ ] No duplicate message delivery on reconnect

**Points:** 3

---

### Epic: Hub Database & Repositories

**Description:**
Implement concrete repository classes for all entities: SpokeRepository, ProjectRepository, JobRepository, MessageRepository. All repositories follow async/await pattern and expose IQueryable for flexibility. Set up transaction management and connection pooling.

**Total Points:** 12
**Target Week:** Week 1

#### Stories

**NEX-13: Implement SpokeRepository**
Create SpokeRepository : ISpokeRepository with AddAsync, GetAsync, ListAsync, UpdateAsync, DeleteAsync (soft delete optional). Handle query optimization: index on Status for status filtering, on LastSeen for timeout checks. Include eager-loading of relationships as needed.

- [ ] CRUD operations all functional
- [ ] Queries use appropriate indexes
- [ ] No N+1 query problems
- [ ] Soft delete supported for audit trail

**Points:** 2

---

**NEX-14: Implement ProjectRepository**
Create ProjectRepository : IProjectRepository. Support querying by SpokeId, by ExternalKey (Jira ticket key), by Status. Handle unique constraint on (SpokeId, ExternalKey). Support pagination for large project lists.

- [ ] All query methods efficient
- [ ] Unique constraint enforced
- [ ] Pagination implemented (skip, take)
- [ ] Can retrieve projects by external key (Jira key)

**Points:** 2

---

**NEX-15: Implement JobRepository**
Create JobRepository : IJobRepository. Support querying by ProjectId, by SpokeId, by Status. Handle date range queries (CreatedAt, StartedAt, CompletedAt) for job history. Support aggregations: count jobs by status per project.

- [ ] All query methods efficient
- [ ] Date range queries fast
- [ ] Status aggregations supported
- [ ] Job history retrievable with pagination

**Points:** 2

---

**NEX-16: Implement MessageRepository and OutputStream repository**
Create MessageRepository for conversational history and OutputStreamRepository for job terminal output. MessageRepository supports filtering by direction, by job. OutputStreamRepository handles sequence-based retrieval and streaming. Both support pagination.

- [ ] Message history queries paginated and ordered by timestamp desc
- [ ] Output stream sequences never have gaps
- [ ] Efficient retrieval for large output logs (millions of chars)
- [ ] Support searching within output (grep-like)

**Points:** 2

---

**NEX-17: Set up transaction management and connection pooling**
Configure EF Core connection pooling (default 128 connections). Implement transaction middleware for multi-step operations (job creation → project update → message log). Handle deadlock retry logic. Set command timeout to 30 seconds.

- [ ] Connection pool configured
- [ ] Multi-operation transactions are atomic
- [ ] Deadlock retry up to 3 times
- [ ] Long-running queries have explicit timeout

**Points:** 2

---

### Epic: Spoke Daemon Foundation

**Description:**
Create the .NET 10 worker service that runs on each spoke machine. Implement configuration loading, outbound WebSocket connection to hub, heartbeat mechanism, and message queue for handling hub-to-spoke commands. Spoke should survive network interruptions gracefully.

**Total Points:** 12
**Target Week:** Week 1

#### Stories

**NEX-18: Initialize Nexus.Spoke solution structure**
Create Nexus.Spoke.sln with main Nexus.Spoke project (.NET 10 worker service) and Nexus.Spoke.Tests project. Add standard dependencies: SignalR client, configuration (appsettings.json + environment variables), logging, background service abstraction.

- [ ] Nexus.Spoke.sln builds cleanly
- [ ] All dependencies added to .csproj
- [ ] Project structure mirrors hub structure
- [ ] Tests project references main project

**Points:** 1

---

**NEX-19: Implement configuration loading from YAML/appsettings**
Create AppConfiguration class that loads from ~/.nexus-spoke/.nexus/config.yaml and environment variables. Support spoke identity (id, name), hub connection details (url, token), workspace path, integrations (Jira, Git, Docker) enable/disable flags, approval mode defaults. Validate required fields on startup.

- [ ] All config fields loaded from file
- [ ] Environment variable overrides work
- [ ] Validation catches missing required fields
- [ ] Configuration exposed as IOptions<AppConfiguration> via DI

**Points:** 2

---

**NEX-20: Implement outbound WebSocket connection to hub**
Create HubConnectionService that establishes persistent WebSocket connection to hub using SignalR .NET client. Implement connection retry with exponential backoff. Register handlers for incoming hub commands (job.assign, job.cancel, message.to_spoke). Expose IsConnected property.

- [ ] Outbound connection to hub works reliably
- [ ] Reconnection on network loss automatic
- [ ] Handlers registered and callable
- [ ] Can send and receive messages without blocking

**Points:** 2

---

**NEX-21: Implement heartbeat sender**
Spoke sends heartbeat to hub every 30 seconds via timer. Heartbeat payload includes spoke id, status (busy/idle), active job count, resource usage (CPU %, memory %). If hub doesn't acknowledge within 10 seconds, log warning. If 3 heartbeats missed, warn user (local log).

- [ ] Heartbeat sent reliably every 30 sec
- [ ] Payload includes current status
- [ ] Missing heartbeats logged
- [ ] Spoke continues operating even if heartbeats dropped

**Points:** 2

---

**NEX-22: Implement command queue and handler dispatch**
Create CommandQueue (thread-safe queue) that receives hub commands. Background worker processes queue sequentially. Implement handler registry: map command types to handler functions. Handlers are async, can update spoke state, trigger worker container launches.

- [ ] Commands processed sequentially (no race conditions)
- [ ] Handlers called with correct payload
- [ ] Errors in one handler don't crash spoke
- [ ] Queue survives temporary disconnections (stores locally)

**Points:** 2

---

**NEX-23: Implement workspace initialization**
Create WorkspaceInitializer service that sets up ~/.nexus-spoke directory structure on startup: .nexus/ (config, memories, agent-state), projects/, templates/. If directories exist, validates structure; if missing, creates them. Logs all setup steps.

- [ ] Directory structure created or validated on startup
- [ ] Appropriate directory permissions set
- [ ] Missing directories created automatically
- [ ] Setup is idempotent (safe to run multiple times)

**Points:** 1

---

### Epic: Hub REST Endpoints (Partial)

**Description:**
Implement REST endpoints for project and job CRUD operations. These support both hub UI and future spoke REST clients. Endpoints are paginated, filtered, and return appropriate HTTP status codes. MVP: no authentication required (LAN-only). Post-MVP: add auth middleware.

**Total Points:** 11
**Target Week:** Week 1

#### Stories

**NEX-24: Implement project CRUD endpoints**
Create ProjectController with POST /api/projects (create), GET /api/projects/{id} (get), GET /api/spokes/{spokeId}/projects (list by spoke), PUT /api/projects/{id} (update status/metadata), DELETE /api/projects/{id} (soft delete). Support filtering by status. Return 404 if not found, 400 if invalid, 201 on create.

- [ ] POST creates project and returns 201 with location header
- [ ] GET returns correct project or 404
- [ ] List endpoint paginated, filtered by status
- [ ] PUT updates only mutable fields
- [ ] No authentication required for MVP

**Points:** 2

---

**NEX-25: Implement job CRUD and status endpoints**
Create JobController with POST /api/jobs (create), GET /api/jobs/{id}, GET /api/projects/{projectId}/jobs (list), POST /api/jobs/{id}/approve, POST /api/jobs/{id}/cancel. Job creation validates project exists. Approval updates status to AwaitingApproval or Running depending on approval mode. Cancel marks as Cancelled and notifies spoke.

- [ ] Job creation validates project and spoke
- [ ] Approval mode respected (immediate vs. plan-review)
- [ ] Cancel message sent to spoke via SignalR
- [ ] Job history queryable by date range

**Points:** 3

---

**NEX-26: Implement job output recording endpoint**
Create POST /api/jobs/{id}/output handler. Spoke streams terminal output chunks (< 8KB each). Each chunk appended to OutputStream table with sequence number and timestamp. Endpoint returns 202 Accepted, doesn't block. Clients subscribe via SignalR for real-time output.

- [ ] Output chunks appended without gaps
- [ ] Sequences never duplicate
- [ ] Output searchable by job ID and sequence
- [ ] Large jobs (GBs of output) handled without OOM

**Points:** 2

---

**NEX-27: Implement message endpoints**
Create MessageController with POST /api/messages (record), GET /api/spokes/{spokeId}/conversation (retrieve history). Support filtering by job, by direction. Conversation endpoint paginated (50 messages per page, most recent first). Messages logged with timestamps and user context.

- [ ] POST records message and broadcasts via SignalR
- [ ] GET retrieves conversation with correct pagination
- [ ] Messages include sender, timestamp, optional job context
- [ ] Conversation history complete and accurate

**Points:** 2

---

### Epic: Hub UI — Dashboard Foundation (MVP, No Auth)

**Description:**
Create Next.js 15 SPA with spoke dashboard and real-time status display. UI uses SignalR client to subscribe to real-time events. **MVP: no authentication required (LAN-only access assumed).** Responsive design for desktop and mobile. Authentication UI added post-MVP.

**Total Points:** 6
**Target Week:** Week 1

#### Stories

**NEX-28: Initialize Next.js 15 project with TypeScript and styling**
Create web/ directory with Next.js 15 scaffold: app router, TypeScript config, Tailwind CSS or equivalent. Add .env.local for hub URL (e.g., `http://192.168.1.100:5000`). Create base layout with header, navigation. Set up build and dev server.

- [ ] Next.js project runs on localhost:3000
- [ ] TypeScript strict mode enabled
- [ ] Tailwind CSS (or equivalent) configured
- [ ] Environment variables load correctly

**Points:** 2

---

**NEX-29: Create main dashboard view with spoke list**
Create pages/dashboard showing list of all spokes. For each spoke, display: name, status (online/offline/busy), last_seen timestamp, active job count, capabilities as badges. Status color-coded (green = online, gray = offline, yellow = busy). Click spoke to see detail view (created in later epic).

- [ ] Dashboard accessible immediately (no login required for MVP)
- [ ] Spoke list loads from GET /api/spokes
- [ ] Status colors and icons correct
- [ ] Last_seen displayed as relative time ("5 minutes ago")

**Points:** 2

---

**NEX-30: Implement real-time spoke status updates via SignalR**
Hub UI connects to SignalR hub on mount. Subscribes to spoke.connected, spoke.disconnected, spoke.status_updated events. When events received, updates local state (re-render). Status changes reflected immediately without polling.

- [ ] SignalR connection established on mount
- [ ] Real-time spoke status updates work
- [ ] No polling fallback (full real-time)
- [ ] Connection loss detected and UI indicates

**Points:** 2

---

**NEX-31: Create spoke detail page stub**
Create pages/spokes/[spokeId] page. For now, display spoke name, ID, status, last_seen, capabilities list. Show project list (projects/{spokeId}/projects endpoint). Show recent jobs (GET /api/projects/{projectId}/jobs). Stub out buttons for actions (create job, approve job, etc.) — no handlers yet.

- [ ] Detail page loads spoke and projects
- [ ] Projects and recent jobs displayed
- [ ] UI responds but buttons don't do anything yet
- [ ] Page layout ready for future additions

**Points:** 2

---

### Epic: Cross-Platform Spoke Support

**Description:**
Extend spoke deployment to Windows, macOS, and Linux using .NET 10 Worker Service abstractions. Implement OS-aware configuration paths, platform-specific service management (systemd, Windows Service, launchd), and cross-platform binaries. Create installation automation for all platforms.

**Total Points:** 17
**Target Week:** Week 1-2

#### Stories

**NEX-32: Implement OS-aware file path resolution for spoke workspace**
Use Environment.GetFolderPath() to resolve spoke workspace location per OS: ~/.nexus on Linux/macOS, %LOCALAPPDATA%\Nexus\ on Windows. Config file location, credentials storage, and log paths follow OS conventions. Cross-platform path utilities abstracted into SpokePaths utility class.

- [ ] SpokePaths utility class created
- [ ] Config path resolves correctly per OS
- [ ] Credentials storage per-OS compliant
- [ ] Log directory initialized and writable

**Points:** 2

---

**NEX-33: Add Windows Service hosting support to spoke**
Implement WindowsServiceLifetime for .NET Worker Service on Windows. Create service installation via sc.exe. Spoke runs as Windows Service (auto-start on boot). Log output to Event Viewer. Service installation/uninstallation integrated into installer script.

- [ ] Worker Service supports Windows hosting
- [ ] sc.exe service installation works
- [ ] Service auto-starts on boot
- [ ] Event Viewer logs visible

**Points:** 3

---

**NEX-34: Add macOS launchd support to spoke**
Create launchd plist configuration (~/Library/LaunchAgents/com.nexus.spoke.plist) for spoke on macOS. Handle log output and startup on login. Plist generation integrated into install script. Launchctl commands for enable/disable/status.

- [ ] Launchd plist created and installed
- [ ] Service starts on login
- [ ] Logs accessible via system tools
- [ ] Install/uninstall script integration

**Points:** 3

---

**NEX-35: Create cross-platform installation script (detect OS, install spoke)**
Develop install-spoke.sh (Bash) that detects OS (uname), selects platform-specific binaries, creates workspace directories per-OS conventions, generates config.yaml with hub URL and token prompts, installs service (systemd/Windows Service/launchd based on OS). Single script works end-to-end on all platforms.

- [ ] OS detection works (uname, uname -s)
- [ ] Platform binaries downloaded/verified
- [ ] Workspace directories created per-OS
- [ ] Config.yaml generated with prompts
- [ ] Service installed and verified

**Points:** 3

---

**NEX-36: Publish platform-specific binaries (win-x64, osx-arm64, linux-x64) in CI/CD**
Build CI/CD pipeline (GitHub Actions) that publishes self-contained .NET 10 Worker Service executables for Windows (x64), macOS (ARM64), and Linux (x64). Output: nexus-spoke-win-x64.exe, nexus-spoke-osx-arm64, nexus-spoke-linux-x64 as release assets. Use `dotnet publish -r <rid> -c Release --self-contained`.

- [ ] GitHub Actions workflow created
- [ ] All three platforms publish successfully
- [ ] Binaries are self-contained
- [ ] Release assets generated and tagged
- [ ] SHA256 checksums provided

**Points:** 3

---

**NEX-37: Document Docker prerequisites per OS (Docker Desktop vs Engine)**
Create docs/docker-prerequisites.md covering: Docker Desktop on Windows (with WSL2), Docker Desktop on macOS (with virtualization), Docker Engine on Linux (native). Worker containers always Linux regardless of host. Troubleshooting for common installation issues. Verify docker CLI and docker daemon are accessible.

- [ ] Windows Docker Desktop setup documented
- [ ] macOS Docker Desktop setup documented
- [ ] Linux Docker Engine setup documented
- [ ] Worker container expectations explained
- [ ] Verification steps provided

**Points:** 2

---

---

## Phase 2 — Workspace & Projects (~1 week, 54 points)

Spokes create and manage local project workspaces. Jira integration allows pulling ticket details. Projects appear on hub UI. Conversational interface lets hub UI send questions to spoke and get responses.

### Epic: Spoke Workspace & Project Management

**Description:**
Spoke can create project folders from Jira tickets, store metadata, and maintain project state. Workspace structure follows the vision document. Spoke notifies hub of new/updated projects via SignalR.

**Total Points:** 14
**Target Week:** Week 2

#### Stories

**NEX-32: Implement project folder creation and metadata storage**
Create ProjectManager service that creates ~/nexus-spoke/projects/{JIRA-KEY}/ folders with .meta/ subdirectory. Store ticket details in .meta/ticket.json (cached from Jira), .meta/status.json (project status), .meta/context.md (assembled context). Create .meta/plan.md (empty initially, filled by agent later).

- [ ] Folders created on disk with correct structure
- [ ] Ticket metadata persisted as JSON
- [ ] Status updated as project progresses
- [ ] Context file prepared for worker prompts

**Points:** 2

---

**NEX-33: Implement Jira integration — fetch ticket details**
Create JiraIntegration service that uses Jira REST API to fetch ticket details by key. Spoke config includes Jira instance URL and token. Extract summary, description, acceptance criteria, issue type, assignee, labels. Cache locally in project .meta/ticket.json. (Jira MCP integration planned for post-MVP.)

- [ ] Ticket fetched successfully from Jira
- [ ] All relevant fields extracted
- [ ] Cached locally for offline reference
- [ ] Error handling if ticket not found or auth fails

**Points:** 3

---

**NEX-34: Implement project creation from hub directive**
Spoke receives job.assign message from hub with project key (e.g., "PROJ-4521"). ProjectManager creates folder, fetches ticket, creates .meta files, and sends project.created event back to hub. Hub records project in database.

- [ ] Spoke creates project folder on hub command
- [ ] Ticket details fetched and cached
- [ ] project.created event sent to hub
- [ ] Hub displays new project on dashboard immediately

**Points:** 2

---

**NEX-35: Implement project status lifecycle and updates**
Project statuses: Planning → Active → (Paused) → Completed or Failed. ProjectManager updates status.json as work progresses. When status changes, ProjectStatusChangedAsync called via SignalR to notify hub. Hub broadcasts to all UI clients.

- [ ] Status transitions are valid (no invalid jumps)
- [ ] Status updates appear on hub dashboard in real-time
- [ ] Status changes logged with timestamp
- [ ] Can pause/resume projects

**Points:** 2

---

**NEX-36: Implement project history and artifact storage**
Create jobs/ subdirectory under each project. Each job gets job-{id}/ folder with prompt.md, output.log, summary.md, status.json. Spoke appends to output.log as worker runs. After job completes, spoke generates summary.md. Hub UI can view job history and artifacts (read-only).

- [ ] Job artifacts stored in correct structure
- [ ] Output log streamed reliably
- [ ] Summaries generated after completion
- [ ] Hub can retrieve and display artifact history

**Points:** 2

---

**NEX-37: Implement memory file initialization and structure**
Create spoke/.nexus/memories/ directory with three files: global.md (cross-project knowledge), codebase-notes.md (repo-specific patterns), decision-log.md (key decisions). Initialize with template headers and examples. Spoke can append to these files as it learns.

- [ ] Memory files created on workspace init
- [ ] Templates provide guidance on content format
- [ ] Spoke can read and write to memory files
- [ ] Memory persists across sessions

**Points:** 1

---

**NEX-38: Implement project listing and filtering (spoke-side)**
Create ProjectLister that returns list of all projects in workspace, with status, summary, last_updated. Support filtering by status. Use for hub UI project list and for spoke's own "what should I work on?" logic.

- [ ] List all projects with metadata
- [ ] Filter by status (planning, active, etc.)
- [ ] Sort by last_updated or creation date
- [ ] Return from API endpoint and local method

**Points:** 2

---

### Epic: Spoke Conversational Interface

**Description:**
Spoke can receive conversational messages from hub UI and respond with context-aware information about its local environment. Spoke uses Claude API internally to generate responses based on local projects, memories, and status.

**Total Points:** 12
**Target Week:** Week 2

#### Stories

**NEX-39: Implement spoke message handler and Claude integration**
Create MessageHandler service that receives message from hub UI. Handler assembles context (current projects, recent jobs, relevant memory excerpts) and sends prompt to Claude API asking it to respond conversationally. Claude response returned to hub via SignalR.

- [ ] Messages from hub received and processed
- [ ] Local context assembled and injected into Claude prompt
- [ ] Claude API called successfully
- [ ] Response streamed back to hub and UI updates

**Points:** 3

---

**NEX-40: Implement context assembly for conversational prompts**
Create ContextAssembler that gathers: current projects (names, statuses), recent job outcomes (summaries), memory excerpts relevant to the query (semantic search or keyword matching), active blockers. Assembled context injected into Claude prompt for richer responses.

- [ ] Context includes recent projects and jobs
- [ ] Relevant memory excerpts selected
- [ ] Assembled context < 4K tokens to stay within budget
- [ ] Context formatted clearly for Claude

**Points:** 2

---

**NEX-41: Implement spoken capability queries**
Hub UI can ask spoke: "What's next in the sprint?" "What projects are active?" "Summarize the last 5 jobs." Spoke retrieves local data, synthesizes with memory, and responds. Responses include actionable suggestions.

- [ ] Spoke responds to natural language queries
- [ ] Responses reflect actual local state
- [ ] Suggestions are specific and actionable
- [ ] Responses delivered within 10 seconds

**Points:** 2

---

**NEX-42: Implement message history and audit logging**
All conversational messages logged locally on spoke (in conversation.log under .nexus/agent-state/). Hub also logs messages in Messages table. Hub UI can view conversation history with a spoke.

- [ ] Conversation history stored and retrievable
- [ ] History searchable by timestamp or keyword
- [ ] Conversation context maintained across sessions
- [ ] Hub UI can display conversation thread

**Points:** 2

---

**NEX-43: Implement approval mode configuration**
Spoke reads approval.mode from config (options: plan_review, manual_approval, full_autonomy). Mode determines job workflow: plan_review = generate plan, send to hub for approval; manual_approval = ask before starting; full_autonomy = start immediately. Mode can be per-spoke or per-project.

- [ ] Config option read and respected
- [ ] Approval mode affects job workflow
- [ ] Hub UI reflects approval mode for each project
- [ ] Can toggle mode from hub UI

**Points:** 2

---

### Epic: Hub UI — Projects & Jobs

**Description:**
Hub UI displays projects and job management. Users can create jobs, approve plans, track progress, and view results.

**Total Points:** 12
**Target Week:** Week 2

#### Stories

**NEX-44: Create project detail page**
Create pages/projects/[projectId]. Display project name, Jira key, status, summary, associated spoke. Show ticket details (summary, description, acceptance criteria) fetched from .meta/ticket.json. Show action buttons: create job, view jobs, view context.

- [ ] Project details load from /api/projects/{id}
- [ ] Ticket details displayed clearly
- [ ] Action buttons present and enabled
- [ ] Real-time status updates work

**Points:** 2

---

**NEX-45: Create job creation dialog and flow**
Add "Create Job" button on project detail. Dialog allows user to select job type (Implement, Test, Refactor, Investigate). Optional approval override (force approval gate on/off). Submit creates job via POST /api/jobs.

- [ ] Dialog appears when clicking "Create Job"
- [ ] Job type selection works
- [ ] Submit triggers POST /api/jobs
- [ ] Success message shows job created

**Points:** 2

---

**NEX-46: Create job detail and output stream view**
Create pages/jobs/[jobId]. Display job metadata (status, type, created/started/completed times). Show live terminal output stream (subscribe to SignalR job.output events). Output is scrollable, searchable, and tail-able (auto-scroll to end).

- [ ] Job details load correctly
- [ ] Output stream loads and displays
- [ ] Real-time output updates appear
- [ ] Can search output by keyword

**Points:** 3

---

**NEX-47: Create approval gate UI for plan review**
If job awaits approval, display generated plan (plan.md) in a review interface. Show "Approve" and "Request Changes" buttons. If approved, job transitions to Running. If changes requested, message back to spoke with feedback.

- [ ] Plan displayed in readable format
- [ ] Approve button sends approval signal
- [ ] Changes can be requested with custom message
- [ ] Spoke receives feedback and adapts

**Points:** 2

---

**NEX-48: Implement job queue and filtering**
Dashboard shows job queue (all pending, in-progress, completed jobs across all spokes). Filter by status, by spoke, by project. Sort by created date, completion time. Pagination for large queues. Real-time updates as jobs progress.

- [ ] Queue view shows all jobs
- [ ] Filters and sorting work
- [ ] Pagination handles large datasets
- [ ] Real-time job status updates

**Points:** 3

---


### Epic: Skills System (Phase 2)

**Description:**
Implement skill file structure and merging logic. Spokes have local CLAUDE.md files (workspace-level config). Projects can have overlay skills. Workers inherit merged skills at launch.

**Total Points:** 13
**Target Week:** Week 2 (parallel with workspace)

#### Stories

**NEX-48: Create spoke-level skills directory structure and CLAUDE.md template**
Create `~/.nexus-spoke/.claude/skills/` directory. Generate default CLAUDE.md template with workspace context placeholders. Template includes: spoke identity, available projects, job orchestration context.

- [ ] Skills directory created with proper permissions
- [ ] CLAUDE.md template generated with correct structure
- [ ] Template includes workspace metadata

**Points:** 3

---

**NEX-49: Implement skill merge logic (spoke-level + project overlay)**
Create SkillMerger service that:
1. Loads spoke-level skills from `~/.nexus-spoke/.claude/skills/`
2. Loads project-level skills from `project/.claude/skills/` (if exists)
3. Merges with precedence: project overlay > spoke > defaults
4. Returns merged CLAUDE.md for injection into worker

- [ ] Merge logic implemented with correct precedence
- [ ] Handles missing skill files gracefully
- [ ] Returns merged markdown for injection

**Points:** 3

---

**NEX-50: Mount merged skills into worker containers at launch**
Update WorkerOrchestrator to:
1. Call SkillMerger to get merged skills
2. Write merged skills to temp file
3. Mount at `/work/.claude/skills.md` (read-only) in worker container
4. Inject into Claude Code CLI via `CLAUDE_CODE_SKILLS` env var

- [ ] Merged skills mounted in worker
- [ ] Worker receives skills via environment/file
- [ ] Workers can reference skills in prompts

**Points:** 3

---

**NEX-51: Create spoke agent skills (workspace management, job orchestration)**
Write skills files for spoke daemon: workspace management (create/list projects), job tracking, conversation management. Include examples of prompts the spoke agent handles.

- [ ] Spoke skills documented and tested
- [ ] Examples provided
- [ ] Can be injected into CC sessions

**Points:** 4

---

### Epic: Conversation Management (Phase 2)

**Description:**
Hub stores conversation records and messages. Spokes support multi-turn conversations via CC `--resume` flag. Messages flow bidirectionally: user → spoke → hub mirroring.

**Total Points:** 15
**Target Week:** Week 2 (parallel with workspace)

#### Stories

**NEX-52: Add Conversation and ConversationMessage tables to hub DB**
Create EF Core entities for Conversation and ConversationMessage. Conversation has: Id, SpokeId (nullable), Title, CreatedAt, UpdatedAt, CcSessionId, MessageCount (denormalized). ConversationMessage has: Id, ConversationId, Role, Content, Timestamp. Add migrations.

- [ ] Entities defined
- [ ] Migrations created and tested
- [ ] Indexes on ConversationId, SpokeId, CreatedAt

**Points:** 2

---

**NEX-53: Implement conversation CRUD REST endpoints**
Implement:
- GET /api/conversations (list, filterable by spokeId)
- GET /api/conversations/{id} (with messages)
- POST /api/conversations (create new)
- DELETE /api/conversations/{id} (soft delete)
- POST /api/conversations/{id}/messages (send message)

- [ ] All endpoints implemented
- [ ] Authentication required
- [ ] Proper error handling and validation

**Points:** 3

---

**NEX-54: Spoke: integrate CC CLI invocation with --resume for multi-turn conversations**
Update spoke daemon to:
1. Receive message from hub via SignalR
2. Invoke `claude-code --resume {ccSessionId} < {message}` as subprocess
3. Wait for CC to complete
4. Capture stdout/stderr and response

- [ ] CC invocation subprocess working
- [ ] Session resume working
- [ ] Response captured and returned

**Points:** 5

---

**NEX-55: Spoke: mirror conversation messages to hub after each exchange**
After CC responds, spoke sends message to hub via POST /api/conversations/{id}/messages with role=assistant and content=CC response.

- [ ] Messages mirrored bidirectionally
- [ ] Timestamps correct
- [ ] Error handling for failed mirror operations

**Points:** 3

---

**NEX-56: Hub UI: conversation list sidebar with spoke grouping**
Add conversation list to left sidebar (below spoke list). Group by spoke, show recent conversations sorted by updated_at desc. Each conversation item shows spoke name, title, message count. Clicking navigates to conversation view.

- [ ] Conversation list renders
- [ ] Grouped by spoke
- [ ] Click navigation working

**Points:** 3

---

**NEX-57: Hub UI: New Thread button and conversation switching**
Add "New Thread" button above conversation list. Clicking opens modal to create conversation (select spoke or hub). Implement conversation view that loads messages and renders them. Support switching between conversations (preserve scroll).

- [ ] New Thread button functional
- [ ] Conversation creation modal
- [ ] Conversation switching with state preservation

**Points:** 3

---

**NEX-58: Hub UI: streaming response display from CC output**
Implement real-time message streaming via SignalR `ConversationMessageReceived` event. When response arrives from spoke, append to message list immediately. Support partial message updates (for streaming long responses).

- [ ] SignalR event received and handled
- [ ] Messages displayed in real-time
- [ ] Scroll management for new messages

**Points:** 5

---

**NEX-59: Add ConversationMessageReceived SignalR event**
Define SignalR event on hub (Hub → Frontend): ConversationMessageReceived with payload conversationId, messageId, role, content, timestamp. Implement broadcasting when spoke sends message.

- [ ] Event defined in hub
- [ ] Broadcasted to all subscribed clients
- [ ] Payload correct and complete

**Points:** 2

---

## Phase 3 — Worker Containers (~1 week, 50 points)

Spokes launch Docker containers to execute jobs using Claude Code CLI. Output streams real-time through hub to UI.

### Epic: Spoke Docker Integration & Worker Launching

**Description:**
Spoke can build/pull Claude Code worker image, launch containers with mounted repo and injected prompt, and stream output. Containers are isolated, disposable, and monitored.

**Total Points:** 18
**Target Week:** Week 3

#### Stories

**NEX-49: Create worker container Dockerfile**
Create worker/Dockerfile. Base image: Ubuntu 24.04 LTS. Install Claude Code CLI, git, and essential build tools (gcc, make, etc.). Entry point: bash script that accepts prompt file, runs Claude Code CLI with that prompt.

- [ ] Dockerfile builds successfully
- [ ] Final image < 2GB
- [ ] Claude Code CLI available in container
- [ ] Git and build tools installed

**Points:** 2

---

**NEX-50: Implement worker container image pull/build logic**
Create WorkerImageManager service. On startup, pull official worker image from container registry (or build from Dockerfile if offline). Support local image caching. If image not found, build locally and tag appropriately.

- [ ] Image pulled or built on first use
- [ ] Pulled image verified (signature check if available)
- [ ] Built image tagged and cached locally
- [ ] Fallback to rebuild if pull fails

**Points:** 2

---

**NEX-51: Implement job worker launcher**
Create WorkerLauncher service that launches Docker container for a job. Mount project repo (read-only), inject prompt.md file, set env vars (JIRA_KEY, PROJECT_ID, etc.). Container runs and streams stdout/stderr back to spoke. Container lifecycle tracked (created, running, completed, error).

- [ ] Container launched with correct mounts
- [ ] Prompt file injected successfully
- [ ] Environment variables set
- [ ] Container exits cleanly after job completes

**Points:** 3

---

**NEX-52: Implement output streaming from container to spoke**
Use Docker SDK to attach to container and stream output. Spoke accumulates output chunks and sends via job.output SignalR message to hub. Hub persists to OutputStream table and broadcasts to UI clients.

- [ ] Output streamed without blocking worker
- [ ] No output lost (all stdout/stderr captured)
- [ ] Streaming works for long-running jobs (hours)
- [ ] Performance: < 500ms latency per output chunk

**Points:** 3

---

**NEX-53: Implement job status lifecycle (queued → running → completed)**
Job starts in Queued status. When worker launched, status → Running, StartedAt timestamp set. When container exits (success), status → Completed, CompletedAt set. If container exits with error, status → Failed. Spoke sends status updates to hub via SignalR.

- [ ] Status transitions happen reliably
- [ ] Timestamps recorded accurately
- [ ] Exit code captured and logged
- [ ] Status changes broadcast to hub

**Points:** 2

---

**NEX-54: Implement job timeout and cancellation**
Jobs have configurable timeout (default 4 hours). If job still running after timeout, worker container is killed and job marked Failed. Hub UI can also send job.cancel command; spoke kills container immediately. Post-job summary includes timeout reason if applicable.

- [ ] Timeout enforced reliably
- [ ] Hub-initiated cancellation works
- [ ] Container cleanup happens (no orphans)
- [ ] Job marked as failed/cancelled with reason

**Points:** 2

---

**NEX-55: Implement worker resource limits**
Docker containers launched with resource limits: CPU (2 cores), memory (8GB), disk write (100GB per job, to prevent infinite loops). If limits exceeded, container killed and job marked Failed with explanation.

- [ ] Resource limits enforced
- [ ] Container killed if limits exceeded
- [ ] Job marked failed with clear reason
- [ ] No resource exhaustion on host

**Points:** 2

---

### Epic: Prompt Assembly & Context Injection

**Description:**
Before launching a worker, spoke assembles a complete prompt from multiple sources: base template, ticket details, implementation plan, relevant memory excerpts, and project history.

**Total Points:** 9
**Target Week:** Week 3

#### Stories

**NEX-56: Create worker prompt base template**
Create templates/worker-prompt-base.md with standard instructions for Claude Code worker. Include: coding conventions (naming, formatting), expected output format (commit message, PR template), how to handle errors, when to ask for help. Template is parameterized: {REPO_PATH}, {TICKET_SUMMARY}, {ACCEPTANCE_CRITERIA}, etc.

- [ ] Template created and checked in
- [ ] All placeholders documented
- [ ] Template is comprehensive but concise
- [ ] Examples provided for each section

**Points:** 1

---

**NEX-57: Implement prompt assembly pipeline**
Implement prompt assembly that prepares job context with mounted skill directories and CLAUDE.md. Steps: (1) load job base template, (2) inject ticket details (summary, description, AC), (3) mount job-specific CLAUDE.md to container, (4) mount skill directory containing job context, (5) inject project history and prior job summaries. Assembled prompt delivered to CC container with proper mounts.

- [ ] Skill directory properly mounted
- [ ] CLAUDE.md for job accessible
- [ ] All necessary context mounted
- [ ] Mounted paths available to CC tools

**Points:** 3

---

**NEX-58: Implement memory excerpt selection**
Create SkillSelector that determines which skills from the skill directory are most relevant for a job. Given a job (type=Implement, description=...), select skills by matching tags/keywords. Prioritize recently-updated skills and those matching project type. Skill directory remains mounted for full access.

- [ ] Relevant skills identified
- [ ] Skills matched to job type
- [ ] Recent/active skills prioritized
- [ ] Fallback to all skills if needed

**Points:** 2

---

**NEX-59: Implement project history injection**
Create ProjectHistoryInjector that, given current project, retrieves summaries of prior jobs (if any) and injects into prompt. Format: "Previous jobs on this project: [list of summary.md from prior jobs]". Helps worker learn from past mistakes.

- [ ] Prior job summaries retrieved
- [ ] Injected in readable format
- [ ] Total prompt stays under budget
- [ ] Worker can reference prior work

**Points:** 2

---

**NEX-60: Implement plan template and generation (stub)**
Create templates/plan-template.md. For now, this is a stub—spoke doesn't generate plans yet (that comes in Phase 4). But infrastructure is in place: when plan generation is added, plan-template.md will be filled with agent-generated implementation plan, and PromptAssembler will inject it into worker prompt.

- [ ] Template created with placeholders
- [ ] PromptAssembler can inject plan if present
- [ ] Plan file format documented
- [ ] Ready for Phase 4 plan generation

**Points:** 1

---

### Epic: Hub UI — Job Execution & Monitoring

**Description:**
Hub UI shows job queue, allows job creation/approval, and streams live output.

**Total Points:** 8
**Target Week:** Week 3

#### Stories

**NEX-61: Implement real-time job output streaming to UI**
Hub UI page shows live terminal output. Subscribe to SignalR job.output events. Append each chunk to displayed output. Auto-scroll to bottom. Preserve ANSI color codes (render colored text).

- [ ] Output appears in real-time
- [ ] No lag (< 1s from spoke to UI)
- [ ] Colors render correctly
- [ ] Can copy/select output text

**Points:** 2

---

**NEX-62: Add job controls (pause, cancel, retry)**
Add buttons to job detail page: Cancel Job, Retry Job. Cancel sends job.cancel via SignalR. Retry creates new job with same parameters. Confirm before destructive actions.

- [ ] Cancel button stops running job
- [ ] Retry creates new job
- [ ] Confirmation dialogs present
- [ ] Status updates reflect action immediately

**Points:** 2

---

**NEX-63: Create job completion summary display**
After job completes, display summary.md (generated by spoke). Include: what was done, branches/commits created, any issues or blockers, next steps. Link to generated PR (if applicable). Export summary as artifact.

- [ ] Summary displayed after job completes
- [ ] Formatted readably (markdown rendering)
- [ ] Links to PRs/artifacts work
- [ ] Summary can be copied/saved

**Points:** 2

---

**NEX-64: Implement job history timeline per project**
Project detail page shows timeline of all jobs (past and in-progress). Each job shows type, status, duration, summary snippet. Click to view full job details. Filter by status or type.

- [ ] Timeline shows all jobs in order
- [ ] Click-through to job detail works
- [ ] Filter and sorting options present
- [ ] Mobile-friendly (vertical timeline)

**Points:** 2

---

---

## Phase 4 — Intelligence (~1-2 weeks, 91 points)

Spoke becomes genuinely intelligent: generates implementation plans, maintains and uses memory, can self-direct based on directives like "work through sprint backlog".

### Epic: Spoke Memory System

**Description:**
Spoke reads, writes, and summarizes memory files. Memory grows over time as spoke learns patterns, conventions, and outcomes. Memory is injected into worker prompts and conversational responses.

**Total Points:** 16
**Target Week:** Week 4

#### Stories

**NEX-65: Implement memory file readers and writers**
Create MemoryManager with readGlobal(), readCodebaseNotes(), readDecisionLog() methods. Also write-append methods: appendGlobal(), appendCodebaseNotes(), appendDecisionLog(). Append operations add timestamped entries with content. Files are human-readable markdown.

- [ ] All three memory files readable and writable
- [ ] Appends are atomic (no partial writes)
- [ ] Timestamps added automatically
- [ ] Files remain readable and editable manually

**Points:** 2

---

**NEX-66: Implement memory summarization and cleanup**
Implement automated condensing and archiving of memory files. Memory files grow as jobs complete and decisions are logged. Periodically (weekly or when file > 100KB), old entries are archived to dated backup files while recent entries remain. Keeps files manageable and focused on current context.

- [ ] Archives created dated weekly
- [ ] Recent entries preserved in main files
- [ ] Archived entries searchable/retrievable
- [ ] No data loss, files remain readable

**Points:** 3

---

**NEX-67: Implement decision-log updates after job completion**
After job completes, spoke analyzes outcome and logs significant decisions: "Decided to use transaction for X because Y (job PROJ-4521, date, result: success)". Decision log becomes a searchable KB of why things were done a certain way.

- [ ] Decisions logged after successful jobs
- [ ] Include context (ticket key, date, outcome)
- [ ] Support searching decision log
- [ ] Decisions inform future job prompts

**Points:** 2

---

**NEX-68: Implement codebase-notes updates**
Spoke can append to codebase-notes.md based on learnings from jobs: "Service X uses transactional scope wrapper", "Tests in /tests/unit folder", "Naming convention: prefix_action_subject". Notes built from job outcomes and manual input.

- [ ] Notes auto-updated after jobs
- [ ] Include specific examples and locations
- [ ] Searchable and injectable into prompts
- [ ] Prevent duplicate entries (idempotency)

**Points:** 2

---

**NEX-69: Implement global memory for cross-project learnings**
Global memory captures team conventions, environment setup, common pitfalls: "Always run fmt.sh before commit", "VPN required for Azure access", "Flaky tests in feature-x". Shared across all projects on spoke.

- [ ] Global memory populated from jobs
- [ ] Applicable to all future projects
- [ ] Injected into all worker prompts
- [ ] Human-editable and maintained

**Points:** 2

---

**NEX-70: Implement memory injection into conversation responses**
When spoke responds conversationally to hub, it includes relevant skill-based context: CLAUDE.md for the active job mounted as MCP, custom MCP tools from skill directory available. Responses reference this context: "Per the CLAUDE.md for this job, I recommend...". Skills and mounted context make responses consistent and informed.

- [ ] CLAUDE.md referenced in responses
- [ ] Skill context available via MCP
- [ ] Responses cite which context was used
- [ ] User can see which skills/CLAUDE.md applied

**Points:** 3

---

### Epic: Plan Generation & Approval

**Description:**
Spoke can generate implementation plans for tickets (with Claude API). Plans are reviewed and approved by hub user before work starts. Approved plans guide worker execution.

**Total Points:** 16
**Target Week:** Week 4-5

#### Stories

**NEX-71: Implement plan generation from ticket details**
Create PlanGenerator service. Given a job (ticket summary, description, AC, repo context, relevant memory), prompt Claude to generate a detailed implementation plan. Plan includes: approach, steps, potential gotchas, testing strategy. Saved to .meta/plan.md.

- [ ] Plan generated for new jobs
- [ ] Plan is specific and actionable
- [ ] Includes testing strategy
- [ ] Saved to project .meta/ folder

**Points:** 3

---

**NEX-72: Implement plan review and approval flow**
When plan generated, job status → AwaitingApproval. Hub UI displays plan with Approve/Revise buttons. User can approve immediately or request changes (send message to spoke with feedback). If approved, status → Running, worker launches. If changes, spoke regenerates plan.

- [ ] Hub UI shows pending approval
- [ ] Plan clearly displayed
- [ ] Approve button exists and works
- [ ] Changes flow back to spoke

**Points:** 3

---

**NEX-73: Implement plan-based worker prompt injection**
After plan approval, PromptAssembler injects approved plan into worker prompt: "Here's the approved plan. Follow it. If you encounter issues, ask for help." Worker executes based on plan but can deviate if it discovers better approach (and explains why).

- [ ] Approved plan injected into worker prompt
- [ ] Worker references plan during execution
- [ ] Deviations are noted and logged
- [ ] Plan helps guide worker output

**Points:** 2

---

**NEX-74: Implement manual plan editing**
Hub UI allows user to edit plan before approval: reword steps, add notes, remove/add sections. Edited plan saved. User approves edited version. Spoke sees approved plan (whether auto-generated or edited).

- [ ] Plan editable in UI
- [ ] Changes saved
- [ ] User can approve edited plan
- [ ] Worker receives final plan

**Points:** 2

---

**NEX-75: Implement plan caching and reuse**
Similar jobs (same ticket type, same repo) might have similar plans. Spoke checks if similar plan exists locally. If so, offers to reuse with minimal changes. Saves time and maintains consistency.

- [ ] Similar plans detected
- [ ] User option to reuse
- [ ] Minor edits made quickly
- [ ] Time saved on plan generation

**Points:** 2

---

**NEX-76: Implement plan metrics and success tracking**
After job completes, compare outcome to plan: what worked, what didn't, unexpected issues. Log findings. Use to improve future plans and memory. Track plan-to-outcome correlation over time.

- [ ] Plan outcomes tracked
- [ ] Deviations logged with explanations
- [ ] Success rate metrics gathered
- [ ] Insights feed back into memory

**Points:** 2

---

### Epic: Autonomous Job Orchestration

**Description:**
Spoke can receive high-level directives ("work through sprint backlog") and autonomously create/execute jobs with minimal user intervention. Implements approval gates and self-awareness about capacity.

**Total Points:** 16
**Target Week:** Week 4-5

#### Stories

**NEX-77: Implement project directive handler**
Spoke receives directive from hub UI or conversational interface: "Work through sprint backlog" or "Implement all unstarted tickets". Spoke queries Jira for matching tickets, creates projects for each, and queues jobs. Can be set to auto-execute or await approval.

- [ ] Directives parsed and understood
- [ ] Matching tickets fetched from Jira
- [ ] Projects created for each ticket
- [ ] Jobs queued in priority order

**Points:** 2

---

**NEX-78: Implement job prioritization and queueing**
PriorityQueue holds pending jobs. Spokes can: prioritize by assignee preference, by ticket priority, by dependencies (can't start until blocker done), by estimated complexity. User can reorder queue from hub UI. Spoke executes jobs in queue order.

- [ ] Jobs queued and ordered
- [ ] Priority sortable by multiple factors
- [ ] Dependencies respected
- [ ] Hub UI allows manual reordering

**Points:** 3

---

**NEX-79: Implement batch job approval**
User creates batch of jobs (e.g., 5 high-priority tickets). Each job has plan. User reviews all plans at once, approves batch. Spoke executes jobs sequentially (not in parallel yet) with brief pause between.

- [ ] Batch creation from directive
- [ ] All plans shown for review
- [ ] Batch-level approval
- [ ] Sequential execution with status updates

**Points:** 2

---

**NEX-80: Implement autonomy settings per spoke**
Config file allows setting autonomy level: FULL (work through backlog with no approval), PLAN_REVIEW (generate plan, await approval, then start), FULL_MANUAL (every job awaits explicit approval). Dynamically adjustable from hub UI.

- [ ] Config option set and respected
- [ ] Hub UI can toggle autonomy
- [ ] Approval flow changes per setting
- [ ] Falls back to conservative mode on errors

**Points:** 2

---

**NEX-81: Implement workload monitoring and backpressure**
Spoke tracks active jobs and resource usage. If running 3+ jobs or CPU > 80%, new jobs pause in queue. Spoke informs hub of backpressure. As jobs complete, queue resumes. Prevents spoke overload.

- [ ] Active job count tracked
- [ ] Resource monitoring (CPU, memory)
- [ ] Backpressure applied automatically
- [ ] Hub UI shows why jobs are paused

**Points:** 2

---

**NEX-82: Implement proactive project suggestions**
Spoke can offer suggestions to user: "PROJ-4521 is done. Next highest priority is PROJ-4523. Should I create a project and generate a plan?" Hub user can say "yes" and spoke starts, or "no, work on X instead".

- [ ] Next-priority ticket identified
- [ ] Suggestion sent to hub UI
- [ ] User can approve or override
- [ ] Spoke responds to user choice

**Points:** 2

---

**NEX-83: Implement failure handling and retries**
If job fails, spoke analyzes failure (from output log and error), decides if retryable (dependency failure, timeout, intermittent error). Retryable failures auto-retried (up to 3 times). Non-retryable failures escalated to user with summary.

- [ ] Failures analyzed for root cause
- [ ] Retryable failures retried automatically
- [ ] Max 3 retries, then manual escalation
- [ ] User notified of persistent failures

**Points:** 3

---

### Epic: Awaiting Input Queue

**Description:**
Implement a prioritized queue for pending actions that require human input or approval. These include plan reviews, pre-execution gates, post-execution validations, and PR comment resolutions. Hub provides real-time visibility and quick-action workflows.

**Total Points:** 26
**Target Week:** Week 4-5

#### Stories

**NEX-84: Create PendingAction database entity and migration**
Add PendingAction entity to DbContext with fields: Id (GUID), SpokeId (FK), ProjectId (FK), JobId (FK), Type (enum: PlanReview, PreExecution, PostExecution, PrReview), Status (Pending/Approved/Rejected/Resolved), Priority (1-5), CreatedAt, ResolvedAt, Metadata (JSON for context). Create EF Core migration.

- [ ] Entity model created with all fields
- [ ] Foreign keys to Spoke, Project, Job
- [ ] Type and Status enums defined
- [ ] Migration created and tested
- [ ] Indexes on SpokeId, Status, Priority for query performance

**Points:** 2

---

**NEX-85: Implement PendingAction service (create, resolve, list across spokes)**
Create PendingActionService with methods: CreateAsync(spoke, type, priority, metadata), ResolveAsync(actionId, approved, resolution), ListBySpokeAsync(spokeId, status), ListAllAsync(filter by priority/status). Service also queries related Spoke/Project/Job for context.

- [ ] All service methods implemented
- [ ] Queries include necessary joins for context
- [ ] Filtering and sorting work correctly
- [ ] Timestamps set automatically

**Points:** 3

---

**NEX-86: Add GET /api/pending-actions and POST /api/pending-actions/{id}/resolve endpoints**
Create PendingActionController with GET /api/pending-actions (list all, paginated, filterable by status/priority), and POST /api/pending-actions/{id}/resolve (accept approval/rejection decision with optional message). Returns 200 on resolve.

- [ ] GET endpoint lists with pagination
- [ ] Filtering by status and priority works
- [ ] POST validates action exists and is Pending
- [ ] Resolve updates status and broadcasts event

**Points:** 2

---

**NEX-87: Add PendingActionCreated/Resolved SignalR events**
NexusHub broadcasts PendingActionCreated (when action queued) and PendingActionResolved (when approved/rejected). Clients subscribe and update pending action list in real-time. Events include full action context.

- [ ] Events broadcast to all connected clients
- [ ] Payload includes action details
- [ ] Hub UI can subscribe to these events
- [ ] Timestamp and resolution details included

**Points:** 2

---

**NEX-88: Spoke: emit PendingAction events for plan-review, pre-execution, post-execution gates**
When job reaches approval gate (plan ready, pre-execution check, post-execution validation), spoke creates PendingAction and sends event to hub. Spoke waits for resolution signal before proceeding. Gates are configurable per project/autonomy setting.

- [ ] Gates create PendingAction correctly
- [ ] Spoke waits for resolution
- [ ] Hub sends back approval/rejection signal
- [ ] Gates respect autonomy settings

**Points:** 3

---

**NEX-89: Hub UI: Build Awaiting Input page with prioritized queue**
Create pages/awaiting-input showing queue of all pending actions across all spokes. Sort by priority (high-to-low), then by age. For each item: spoke name, project key, action type, timestamp, brief context. Click to expand detail.

- [ ] Page loads all pending actions
- [ ] Sorted by priority and age
- [ ] Shows spoke and project context
- [ ] Real-time updates via SignalR

**Points:** 5

---

**NEX-90: Hub UI: Add pending action badge count to navigation**
Navigation header shows badge with count of pending actions. Badge color indicates if any are high-priority. Clicking badge navigates to Awaiting Input page. Updates in real-time when new actions arrive.

- [ ] Badge shows count
- [ ] Color indicates priority
- [ ] Clickable to navigate
- [ ] Updates via SignalR events

**Points:** 2

---

**NEX-91: Hub UI: Quick-action buttons (approve/reject/respond) on pending items**
Each pending action has action buttons: Approve (green), Reject (red), Respond (blue for messages/changes requested). Approve/Reject are one-click. Respond opens modal for text input. After action, item resolves and disappears.

- [ ] Buttons visible on each item
- [ ] Approve/Reject submit immediately
- [ ] Respond opens modal with text area
- [ ] Actions sent to spoke via SignalR

**Points:** 3

---

**NEX-92: Hub UI: Mobile-optimized Awaiting Input view**
Awaiting Input page responsive for mobile (< 768px). Stack items vertically, make buttons touch-friendly (40px+), use slide-out modals instead of overlays. Test on actual devices.

- [ ] Page renders correctly on mobile
- [ ] Touch targets >= 40px
- [ ] No horizontal scroll
- [ ] Modal interactions work on touch

**Points:** 3

---

### Epic: PR Monitoring & Auto-Resolution

**Description:**
Spokes monitor pull requests in GitHub/GitLab for comments. Comments are classified using Claude to determine if they're actionable. Actionable comments trigger auto-fix flows; non-actionable comments get explanatory responses. Ambiguous comments route to PendingAction queue.

**Total Points:** 33
**Target Week:** Week 4-5

#### Stories

**NEX-93: Spoke: Create IPullRequestProvider abstraction and GitHub implementation**
Define IPullRequestProvider interface with methods: GetPullRequestsAsync(repo), GetCommentsAsync(prNumber), PostCommentAsync(prNumber, comment), GetDiffAsync(prNumber). Implement GitHubPullRequestProvider using GitHub REST API with authentication via token.

- [ ] Interface defined clearly
- [ ] GitHub implementation uses official SDK or REST
- [ ] Authentication with token works
- [ ] Methods handle errors gracefully

**Points:** 3

---

**NEX-94: Spoke: Implement PR polling background service (configurable interval, default 15 min)**
Create PullRequestPollingService (background IHostedService) that runs periodically (default 15 min, configurable). Queries all configured repos for new/updated PRs and comments since last poll. Triggers comment analysis pipeline.

- [ ] Service runs on interval
- [ ] Interval configurable
- [ ] Tracks last poll time
- [ ] Handles service restarts gracefully

**Points:** 3

---

**NEX-95: Spoke: Build PR comment classification pipeline using Claude API**
Create CommentAnalysisService that sends PR comment + PR context (title, description, code diff snippet) to Claude. Claude classifies: ACTIONABLE (specific fix requested), NON_ACTIONABLE (praise/question/discussion), AMBIGUOUS (might need human review). Returns classification + reasoning.

- [ ] Sends appropriate context to Claude
- [ ] Receives structured classification
- [ ] Includes confidence/reasoning
- [ ] Handles Claude API errors

**Points:** 5

---

**NEX-96: Spoke: Auto-fix flow — classify actionable comment → create fix job → push → respond**
If comment classified ACTIONABLE, spoke creates job to implement fix. Job prompt includes comment + PR context. After job completes, spoke pushes changes to PR branch (new commit). Spoke then posts reply thanking commenter and linking to commit.

- [ ] Job creation triggered from comment
- [ ] Diff from job pushed to PR branch
- [ ] Reply posted after push succeeds
- [ ] Commit message references PR and comment

**Points:** 5

---

**NEX-97: Spoke: Handle non-actionable comments — respond with explanation**
If comment classified NON_ACTIONABLE, spoke responds politely: "Thanks for the feedback. This looks like discussion/praise, not a fix request. Marking resolved." Helps keep PR clean and communication clear.

- [ ] Response posted to comment
- [ ] Mark comment thread as resolved (if supported)
- [ ] Tone is professional and appreciative

**Points:** 3

---

**NEX-98: Spoke: Route ambiguous comments to PendingAction queue (PrReview gate)**
If comment classified AMBIGUOUS, don't auto-fix. Instead, create PendingAction (type: PrReview, priority: medium). Hub user reviews and responds ("this is actionable, do X" or "not actionable, skip"). Spoke waits for resolution.

- [ ] AMBIGUOUS comments create PendingAction
- [ ] Action includes comment text and context
- [ ] Spoke waits for hub resolution
- [ ] Hub response routed to PR comment

**Points:** 2

---

**NEX-99: Spoke: PR monitoring configuration in config.yaml**
Add pr_monitoring section to config.yaml: enabled (bool), providers (list of { type: "github" or "gitlab", org, repos, token }), polling_interval_minutes (default 15), auto_fix_enabled (bool, default true). Config loaded and applied on startup.

- [ ] Config section documented
- [ ] Multiple repos supported per provider
- [ ] Tokens loaded from env if prefixed with $
- [ ] Validation catches missing required fields

**Points:** 2

---

**NEX-100: Hub: Add PR monitoring SignalR events (PrCommentDetected, PrCommentResolved, PrFixJobCreated)**
NexusHub broadcasts events: PrCommentDetected (new comment found), PrCommentResolved (comment addressed), PrFixJobCreated (auto-fix job started). Events include PR number, repo, comment summary, classification, action taken.

- [ ] Events broadcast to all clients
- [ ] Payload includes comment and PR context
- [ ] Hub UI can subscribe
- [ ] Timestamp and spoke context included

**Points:** 2

---

**NEX-101: Hub: Add GET /api/spokes/{id}/pull-requests endpoint**
Create endpoint that lists recent PR activity for a spoke: PRs monitored, comments found, auto-fixes applied, pending reviews. Paginated, sortable by date/status. Returns summary + detail.

- [ ] Lists PRs monitored by spoke
- [ ] Includes comment count per PR
- [ ] Shows classification and actions taken
- [ ] Paginated and filtered

**Points:** 2

---

**NEX-102: Hub UI: PR activity in spoke detail view and activity feed**
Add section to spoke detail page showing recent PR activity: "3 PRs monitored, 5 comments processed, 2 auto-fixes applied". Link to detailed PR activity view. Activity feed shows: comment detected, auto-fix started, response posted. Updates in real-time.

- [ ] Activity section visible on spoke detail
- [ ] Activity feed shows PR events
- [ ] Links to GitHub/GitLab MRs
- [ ] Real-time updates via SignalR

**Points:** 3

---

**NEX-103: Spoke: Add GitLab MergeRequestProvider implementation**
Implement GitLabMergeRequestProvider with same interface as GitHub. Uses GitLab REST API for repos, merge requests, comments. Handles group/project routing. Token-based auth (personal or project tokens).

- [ ] Uses GitLab API correctly
- [ ] Authentication works
- [ ] Handles multi-organization scenarios

**Points:** 3

---


### Epic: Hub Meta-Agent (Phase 4)

**Description:**
Hub runs its own Claude Code instance (meta-agent) that answers cross-system queries: "What's the status across all spokes?" "Summarize completed work this week." The meta-agent uses hub-local tools (proxied over SignalR) to query spokes and access hub database. It can reason across multiple spokes and generate summaries. Hub NEVER connects to spoke MCPs directly.

**Total Points:** 18
**Target Week:** Week 4 (parallel with spoke memory)

#### Stories

**NEX-73: Implement hub CC meta-agent with per-message invocation**
Create HubMetaAgent service that:
1. Runs Claude Code process per user message (not persistent session initially)
2. Loads hub-level CLAUDE.md skills
3. Passes message + accessible spoke info
4. Captures response and stores in hub-level "meta" conversation

- [ ] CC invocation working in hub context
- [ ] Skills loaded correctly
- [ ] Responses captured and stored

**Points:** 5

---

**NEX-74: Create hub-local tools for spoke queries (via SignalR proxy)**
Implement hub-local tools that query spokes over SignalR (NOT MCP connections):
- `query_spoke(spoke_id, question)` — send question to spoke, get response (proxied via SignalR)
- `list_spokes()` — list all connected spokes (from hub database)
- `list_all_jobs(status)` — aggregate jobs from database
- `search_projects(query)` — search projects in database
- `get_timeline()` — recent activity from database

- [ ] Hub-local tools implemented (NOT spoke MCPs)
- [ ] query_spoke sends SignalR message and waits for response
- [ ] Spoke receives query, responds via SignalR
- [ ] Error handling for disconnected spokes
- [ ] Correlation IDs match request/response

**Points:** 5

---

**NEX-75: Create hub-level skills for cross-system reasoning**
Write hub-level CLAUDE.md skills that teach meta-agent to:
- Use hub-local tools to query spokes and database
- Synthesize summaries from query responses
- Identify bottlenecks and high-priority items
- Reason across multiple spokes
- Generate proactive insights

- [ ] Hub skills documented
- [ ] Examples of tool usage provided
- [ ] Skills can be injected into hub CC

**Points:** 3

---

**NEX-76: Implement SignalR spoke.query and spoke.query_response events**
Add new SignalR events for hub-to-spoke queries:
- Hub sends: `spoke.query` { correlationId, query, context }
- Spoke responds: `spoke.query_response` { correlationId, response, metadata }
- Implement timeout handling (10s default)
- Register correlation ID handlers on hub

- [ ] SignalR events defined and working
- [ ] Correlation ID matching implemented
- [ ] Timeout handling in place
- [ ] Spoke query handler processes requests

**Points:** 5

---

**NEX-77: Hub UI: hub-level conversation view (talk to meta-agent)**
Create separate conversation interface for hub-level meta-agent. User can ask questions, view system-wide summaries. Messages displayed in same format as spoke conversations but labeled as [Hub Meta].

- [ ] Hub conversation list item prominent
- [ ] Conversation view works
- [ ] Messages display correctly

**Points:** 3

---

**NEX-77: Implement spoke handlers for incoming spoke.query events**
On the spoke side, implement handler for incoming `spoke.query` events:
1. Spoke receives query with correlationId
2. Optionally invokes local CC with spoke-local MCPs (Jira, Git) to reason about query
3. Or reads local state directly (projects, jobs, memory)
4. Responds via `spoke.query_response` with matching correlationId

- [ ] SignalR handler registered on spoke
- [ ] Query processing logic implemented
- [ ] Response sent back with correct correlationId
- [ ] Supports both CC invocation and state-read queries

**Points:** 3

---

---

## Phase 5 — Polish & MVP Launch (~1 week, 19 points)

UI refinement, mobile responsiveness, error handling, documentation, deployment packaging, and open-source readiness.

### Epic: Hub UI Refinement & Mobile

**Description:**
Polish hub UI for production. Responsive design for mobile. Comprehensive error messaging. Optimizations for performance. Deploy-ready Docker images.

**Total Points:** 17
**Target Week:** Week 5-6

#### Stories

**NEX-104: Implement responsive design for mobile**
Audit all hub UI pages for mobile (< 768px viewport). Use CSS Grid/Flexbox for responsiveness. Test on actual phones. Adapt dashboard for small screens: stack spokes vertically, inline buttons, collapse detail sections. Ensure touch-friendly (40px+ touch targets).

- [ ] All pages render correctly on mobile
- [ ] Touch targets >= 40px
- [ ] No horizontal scroll needed
- [ ] Forms usable on small screens

**Points:** 3

---

**NEX-105: Implement comprehensive error handling and user feedback**
All API calls have error boundaries. Network errors show user message ("Connection lost, retrying..."). API errors show message ("Jira token invalid, please reconnect"). Validation errors show inline in forms. Toast notifications for async actions.

- [ ] No JavaScript errors visible to user
- [ ] All API errors have user-friendly messages
- [ ] Network issues handled gracefully
- [ ] Form validation shows errors inline

**Points:** 2

---

**NEX-106: Implement empty states and loading states**
Dashboard with no spokes shows helpful message: "No spokes connected. Set up your first spoke here." Project list with no jobs shows message with link to create job. Loading states show spinners with descriptive text.

- [ ] Empty states for all major views
- [ ] Loading states with spinners
- [ ] Helpful messages guide user
- [ ] No confusing blank screens

**Points:** 2

---

**NEX-107: Implement search across jobs and projects**
Add search bar to dashboard. User can search for project by key or name, job by status or summary. Search is client-side (on initially loaded data) or server-side (if dataset large). Results appear as user types.

- [ ] Search finds projects and jobs
- [ ] Real-time results as user types
- [ ] Clear search results
- [ ] Works on mobile

**Points:** 2

---

**NEX-108: Implement activity timeline**
Create /timeline view showing all events across all spokes: spoke connected, project created, job started, job completed, messages sent. Chronologically ordered, newest first. Filter by spoke or event type. Mobile-friendly vertical timeline.

- [ ] Timeline shows all events
- [ ] Chronologically ordered
- [ ] Filtering works
- [ ] Mobile layout is readable

**Points:** 2

---

**NEX-109: Implement settings page**
Create /settings for user preferences: approval mode defaults, notification preferences (email on job completion?), theme (light/dark), timezone for timestamp display. Settings persisted in database.

- [ ] Settings page accessible
- [ ] User preferences saved
- [ ] Settings applied across UI
- [ ] Works across sessions

**Points:** 2

---

**NEX-110: Implement spoken API documentation page**
Create /docs page in hub UI showing REST API reference: endpoints, request/response format, example curl commands. Also show WebSocket events. Use Swagger/OpenAPI if available (generated from .NET).

- [ ] API endpoints documented
- [ ] Example requests/responses shown
- [ ] WebSocket events listed
- [ ] Docs searchable and accessible

**Points:** 2

---

**NEX-111: Implement spoke profile management (display name, repos, Jira config, integrations)**
Enhance spoke registration to include profile data: display_name, description, repo_config (list of {type, url, credentials}), jira_config {domain, project_keys, token}, integrations {github, gitlab, slack, etc.}. Endpoint PUT /api/spokes/{id}/profile to update. Profile returned in GET /api/spokes/{id}. Support credentials as env var references (e.g., $GITHUB_TOKEN).

- [ ] Profile schema defined in database
- [ ] All profile fields persisted and retrievable
- [ ] Credentials handled securely (never logged)
- [ ] PUT endpoint validates and updates safely
- [ ] GET endpoint includes full profile

**Points:** 3

---

**NEX-112: Implement two-tier spoke query system (cached status + CC-powered queries)**
Create two query methods: (1) query_spoke_status(spoke_id) — returns cached spoke state (online, last_seen, current_jobs) from database, fast (<100ms); (2) query_spoke(spoke_id, question) — sends question to spoke via SignalR for CC-powered response (async, may take seconds). First is for dashboard/monitoring, second for intelligent questions.

- [ ] query_spoke_status returns cached data
- [ ] query_spoke uses SignalR and waits
- [ ] Both documented in API reference
- [ ] Performance metrics documented
- [ ] Different use cases clear in docs

**Points:** 2

---

**NEX-113: Implement pending_commands table and offline command queuing**
Create pending_commands table: spoke_id, command (JSON), created_at, processed_at. When hub sends command (job assignment, approval gate response) to offline spoke, queue to this table. On spoke reconnection, retrieve and process pending commands in order. Mark processed_at when complete. Enable offline resilience.

- [ ] Table created with proper indexes
- [ ] Commands queued on spoke offline
- [ ] Commands processed on reconnect
- [ ] Order guaranteed (FIFO)
- [ ] No commands lost on failure

**Points:** 2

---

### Epic: Documentation & Deployment

**Description:**
Write deployment guides, setup documentation, configuration reference. Package hub and spoke for easy deployment. Create Docker Compose for local dev and k3s manifests for production.

**Total Points:** 17
**Target Week:** Week 5-6

#### Stories

**NEX-114: Write getting-started guide**
Create docs/getting-started.md with step-by-step instructions for first-time user: download, configure hub, configure spoke, connect, create first project, run first job. Assume reader has Docker and .NET SDK.

- [ ] Guide covers all steps
- [ ] Clear screenshots or diagrams
- [ ] Troubleshooting section
- [ ] Time estimate (30 mins start-to-finish)

**Points:** 2

---

**NEX-115: Write hub deployment guide**
Create docs/hub-deployment.md covering: infrastructure requirements (self-hosted Kubernetes cluster), PostgreSQL setup, hub API/UI deployment, Tailscale or Cloudflare Tunnel configuration, SSL/TLS certs, backups. Include Kubernetes manifests and docker-compose files.

- [ ] All deployment options covered
- [ ] Step-by-step instructions
- [ ] Network config (Tailscale, Cloudflare) explained
- [ ] Backup strategy documented

**Points:** 3

---

**NEX-116: Write spoke deployment guide**
Create docs/spoke-deployment.md: system requirements (OS, .NET 8), installation (download, install service), configuration (config.yaml, environment variables), connection to hub, troubleshooting (logs, connectivity checks).

- [ ] Instructions for Linux, Windows, macOS
- [ ] Systemd service file provided
- [ ] Config template with examples
- [ ] Common issues and solutions

**Points:** 2

---

**NEX-117: Write architecture and design document**
Create docs/architecture.md explaining: hub-spoke-worker model, communication patterns (outbound WebSocket), data boundaries (no code on hub), memory system, approval gates. Include architecture diagrams and decision rationale.

- [ ] Architecture clearly explained
- [ ] Diagrams provided
- [ ] Design decisions justified
- [ ] Extensibility points noted

**Points:** 2

---

**NEX-118: Write API reference documentation**
Create docs/api-reference.md with all REST endpoints and SignalR events. Include: endpoint path, HTTP method, authentication, request body, response format, status codes, examples. Auto-generate from OpenAPI if available.

- [ ] All endpoints documented
- [ ] Request/response examples shown
- [ ] Error codes explained
- [ ] Authentication requirements clear

**Points:** 2

---

**NEX-119: Create Docker Compose file for local dev**
Create docker-compose.dev.yml with three services: PostgreSQL (nexus database), hub API, hub UI. Start with `docker-compose -f docker-compose.dev.yml up`. Includes volumes for data persistence and volume mounting for code hot-reload.

- [ ] docker-compose up starts all services
- [ ] Database initialized automatically
- [ ] API and UI accessible on localhost
- [ ] Code changes auto-reload (hot-reload for UI)

**Points:** 2

---

**NEX-120: Create k3s manifests for production deployment**
Create k8s/ directory with manifests: namespace, postgres statefulset (with PVC), hub API deployment, hub UI deployment, services, ingress (for Tailscale/Cloudflare). All use best practices: resource limits, health checks, rolling updates.

- [ ] All manifests created
- [ ] Persistent volume for database
- [ ] Resource limits set
- [ ] Health checks configured
- [ ] Tested on k3s cluster

**Points:** 3

---

**NEX-121: Create standalone spoke installer script**
Create spoke/install.sh script. Downloads .NET 8 SDK (if missing), downloads latest spoke release, creates directories, generates initial config.yaml with prompts for hub URL and token. Sets up systemd service. Runs `./install.sh` and user is done.

- [ ] Script runs end-to-end
- [ ] Prompts for required config
- [ ] Service auto-starts on boot
- [ ] Easy uninstall option

**Points:** 2

---

### Epic: Production Hardening & Open Source

**Description:**
Final polish: error resilience, security hardening, performance optimization, open-source preparation (README, LICENSE, contributing guidelines).

**Total Points:** 0 (stretch goals, scope-reduced for 4-6 week timeline)

#### Stretch Goals (Out of Scope for MVP)

**NEX-122: Implement comprehensive logging and observability**
Structured logging (Serilog) throughout codebase. Hub logs all API calls, SignalR events, job lifecycle. Spoke logs startup, connection, jobs, memory operations. Logs stored locally and optionally shipped to centralized log aggregator.

**Points:** 3 (stretch)

---

**NEX-123: Implement metrics and dashboards**
Prometheus metrics: job success rate, average job duration, spoke uptime, API latency. Grafana dashboards for operations team. Track metrics over time to improve system.

**Points:** 3 (stretch)

---

**NEX-124: Implement rate limiting and quota enforcement**
Hub enforces per-spoke quotas: max 5 concurrent jobs, max 50 jobs/day, max 100GB storage. Rate limiting on API endpoints (10 req/sec per IP). Prevents abuse and runaway costs.

**Points:** 2 (stretch)

---

**NEX-125: Security hardening and penetration testing**
Review: authentication (OAuth token handling), authorization (user can only access own spokes), encryption (TLS for all hub ↔ spoke traffic), input validation (prevent injection), secrets management (no credentials in logs). Penetration test by security expert.

**Points:** 5 (stretch)

---

**NEX-126: Performance optimization**
Profile API and UI. Identify bottlenecks. Optimize: database queries (N+1), caching (Redis for spoke status), UI rendering (virtualization for large job lists). Benchmark: hub should handle 100+ spokes, 1000+ jobs without degradation.

**Points:** 5 (stretch)

---

**NEX-127: Create comprehensive README and contribution guidelines**
README.md with project vision, architecture overview, quick start (link to getting-started), feature list, roadmap, open-source license, contributing guidelines. CONTRIBUTING.md with development setup, code style, pull request process.

**Points:** 2 (stretch)

---

**NEX-128: License selection and legal review**
Choose license (MIT or Apache 2.0 recommended). Add LICENSE file to repo. Review project for any dependencies with incompatible licenses. Add copyright headers to source files.

**Points:** 1 (stretch)

---

**NEX-129: Prepare for open-source release**
Code cleanup and final refactor. Remove any hardcoded secrets/credentials. Write CHANGELOG. Tag release v0.1.0. Create release page on GitHub with download links and release notes. Announce on relevant communities (dev.to, HackerNews, Reddit/r/devops, etc.).

**Points:** 3 (stretch)

---

---

## Phase 6 — Post-MVP: Authentication & Remote Access (Not in MVP scope, ~3-4 days, 18 points)

**Goal:** Add Google OAuth, TLS, and remote access support for production deployment.

**Description:**
Post-MVP phase to harden the hub for remote access. Adds Google OAuth authentication, TLS 1.3 encryption, mTLS for spoke-to-hub, and support for Tailscale/Cloudflare Tunnel. These features are **out of scope for MVP** (which assumes trusted LAN) but are implemented immediately after MVP launch for production use.

### Epic: Post-MVP Authentication & Remote Access

**Total Points:** 18
**Target Timeline:** After Phase 5

#### Stories

**NEX-AU-1: Implement Google OAuth login flow (Hub API)**
Create AuthController with Google OAuth callback handler. User signs in on hub UI → redirected to Google → callback endpoint. Endpoint validates ID token, creates/updates User record, returns JWT. JWT includes user id, email, exp. Whitelist check: only allow specific Google account.

- [ ] OAuth callback validates token signature
- [ ] User record created on first login
- [ ] JWT issued with 24-hour expiration
- [ ] Logout endpoint clears session

**Points:** 3

---

**NEX-AU-2: Implement Google OAuth login page and flow (Hub UI)**
Create pages/auth/login with button to "Sign in with Google". Clicking button redirects to hub OAuth endpoint. After redirect back, store JWT in localStorage and redirect to dashboard. If JWT missing, redirect to login. Implement logout button (clears localStorage, redirects to login).

- [ ] Login page accessible at /auth/login
- [ ] Clicking button starts OAuth flow
- [ ] JWT stored and sent with all API calls
- [ ] Protected routes redirect to login if not authenticated

**Points:** 2

---

**NEX-AU-3: Add TLS 1.3 to hub API and SignalR**
Configure ASP.NET Core to use TLS 1.3. Generate or obtain SSL certificate (self-signed for dev, Let's Encrypt for production). Update appsettings to use https://. Update spoke configuration to support custom hub URLs (with https://). Document certificate setup for self-hosted deployments.

- [ ] Hub API accessible via https://
- [ ] SignalR uses wss:// (WebSocket Secure)
- [ ] Certificate validation works
- [ ] Self-signed certificates supported for development

**Points:** 3

---

**NEX-AU-4: Implement mTLS for spoke-to-hub authentication (Post-MVP)**
Spokes generate or obtain client certificates. On connection, spokes present certificate to hub. Hub validates certificate. Adds second layer of authentication (certificate + pre-shared token). Provides strong identity assurance for spoke registration.

- [ ] Spoke generates/manages client certificate
- [ ] Hub validates client certificate chain
- [ ] Certificate revocation supported (optional)
- [ ] Fallback to token-only if certificate missing (backward compatible)

**Points:** 5

---

**NEX-AU-5: Document remote access setup (Tailscale / Cloudflare Tunnel)**
Document two approaches: (1) Tailscale: install tailscale on hub and spoke machines, mesh network, spoke connects to hub via Tailscale IP. (2) Cloudflare Tunnel: install cloudflared on hub, expose hub on public Cloudflare domain, spoke connects via tunnel. Provide step-by-step guides for both.

- [ ] Tailscale setup documented with screenshots
- [ ] Cloudflare Tunnel setup documented with screenshots
- [ ] Example configs provided for both
- [ ] Troubleshooting guide included

**Points:** 5

---

---

## Implementation Notes

### AI-Accelerated Development Context

This breakdown assumes **Claude Code CLI** is the primary development tool. Timelines and story points are calibrated for AI-assisted development:

- **1 point:** Trivial setup, < 1 hour even with explanation/research
- **2 points:** Small feature, ~1-2 hours, likely one AI session or simple manual coding
- **3 points:** Medium feature, ~half day, may require multiple AI iterations or complex logic
- **5 points:** Large feature, ~full day, significant complexity or integration
- **8 points:** Very large (rare in this breakdown, should be split)

### Critical Path

Minimum viable product (MVP) to get a job running end-to-end on a trusted LAN:

1. **Phase 1** (foundation, no auth): Hub API + SignalR + PostgreSQL + Spoke WebSocket connection. **No authentication, no TLS, LAN-only.**
2. **Phase 3** (workers): Docker integration to actually launch Claude Code.
3. Subset of **Phase 4** (plans): Plan generation and approval (or skip for MVP, worker runs without plan).

**MVP Timeline:** ~2 weeks (Phase 1 + core of Phase 3). Assumes trusted LAN, single user, no external access.

**After MVP (Phase 6):** Add Google OAuth, TLS 1.3, mTLS for spoke auth, Tailscale/Cloudflare support for remote access. Not in MVP scope.

### Dependency Notes

- Phase 1 is the critical foundation. Phase 2–5 depend on Phase 1 being solid.
- Phase 2 (workspace) can start once Phase 1 API is mostly done.
- Phase 3 (workers) depends on Phase 2 (needs project/job context).
- Phase 4 (intelligence) depends on Phases 1–3. Can be done in parallel with Phase 5 UI polish.
- Phase 5 is primarily UI polish and documentation; can start once Phase 3 is demo-able.

### Spike Work (Not Listed as Stories)

Before starting, consider spiking (investigating) these:

1. **Claude Code CLI behavior in container** — Test running Claude Code inside Docker with mocked Jira/repo context. Understanding CLI's input format (prompt files vs. stdin vs. environment) is critical.
2. **SignalR reconnection patterns** — Understand automatic reconnection, backplane needs, and group management for future scaling.
3. **EF Core + PostgreSQL performance** — Test query patterns with realistic data volumes (1000s of jobs) to ensure indexes are sufficient.

---

## Checklist Before Starting

- [ ] Jira project NEX created at eaglebyte.atlassian.net
- [ ] Custom fields added (Story Points, Phase, Acceptance Criteria)
- [ ] Developers invited to project
- [ ] GitHub repository created (eaglebyte/nexus or similar)
- [ ] .NET SDK 10 installed locally
- [ ] Node.js 18+ installed locally
- [ ] Docker installed and running
- [ ] PostgreSQL 16 container tested locally
- [ ] Claude API key obtained and tested
- [ ] Spoke test machine identified (can be same machine for local dev)

---

**Document Version:** 1.0
**Last Updated:** 2026-04-04
**Owner:** Claude Code
