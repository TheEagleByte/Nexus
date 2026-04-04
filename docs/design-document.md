# Nexus — Design Document

**Version:** 1.0
**Last Updated:** 2026-04-04
**Status:** Design Phase

---

## 1. Overview

Nexus is a self-hosted, hub-and-spoke platform for orchestrating persistent AI coding agents across multiple machines. The Hub runs on a local machine via Docker Compose and provides a unified command center for managing spokes — long-running daemons on each work machine on the same LAN (e.g., a primary dev workstation, a secondary machine, a local server). Each spoke runs autonomous Docker-based workers that execute coding tasks via Claude Code CLI, with full visibility and approval gates from a responsive Next.js dashboard.

**Why this exists:** Users work across multiple machines and projects. Each context has its own repositories, credentials, and ticketing systems. Today, using Claude Code means manually spinning up sessions, re-explaining context, and losing continuity. Nexus gives persistent, intelligent agents on each machine that remember codebase conventions, maintain institutional knowledge, and coordinate work across all projects — controllable from a single dashboard on any device.

**Key constraint:** This is purpose-built for single-user operation on a trusted LAN with the security model to match. No multi-tenancy, no shared access, no external users. MVP assumes no authentication (trusted LAN environment).

---

## 2. Goals & Non-Goals

### Goals

- Enable fully persistent AI coding agents on multiple machines that work autonomously or with human approval.
- Maintain a unified view of all projects and work across multiple machines from a single dashboard (desktop and mobile).
- Build a conversational interface to agents — ask questions, assign work, review plans, approve execution.
- Establish a memory system that captures codebase conventions, decisions, and patterns — injected into worker prompts for better context and consistency.
- Support configurable approval gates (plan review, PR review, full autonomy) scoped by spoke, project, or job.
- Ensure spokes can operate behind firewalls, NATs, and VPNs (outbound-only WebSocket communication).

### Non-Goals

- Multi-tenancy or shared workspace. Nexus is single-user only.
- Direct credential storage in the hub. Credentials live on spokes; the hub has no knowledge of external system credentials.
- Real-time code-review tooling. The hub sees summaries and terminal output, not source code.
- Plugin marketplace or community extensions in Phase 1. Extensibility is planned but not a launch goal.
- Voice interface or advanced analytics in Phase 1.

---

## 3. Core Concepts

### 3.1 The Hub

Central command center running on self-hosted infrastructure. Provides:

- **Dashboard** — Real-time view of all spokes, projects, jobs, and activity. Responsive design for desktop and mobile.
- **Conversational Interface (Hub Meta-Agent)** — Interact with a hub-level Claude Code instance that can answer cross-system questions, aggregate status across all spokes, and surface insights proactively. The hub meta-agent uses hub-local tools (not MCP connections) to query spokes via SignalR.
- **Job Orchestration** — Create, queue, approve, and monitor jobs across all spokes.
- **Message Log** — Conversation history with all spokes and the hub meta-agent, searchable.
- **Data Persistence** — PostgreSQL database for spoke registry, project catalog, job history, conversation history, and message logs.

**Data Boundary:** The hub does not store credentials, source code, or proprietary artifacts. Only metadata, summaries, status, and terminal output flow here. Spokes are gatekeepers for all sensitive data. CC messages flow freely between hub and spoke; the boundary is simply that full code diffs are not displayed on the hub UI.

**Hub CC Instance:** The hub also runs a persistent Claude Code instance (the "meta-agent") that handles cross-system reasoning. When users interact with the hub, they're interacting with this meta-agent. Its system prompt includes a summary of all spoke profiles so users can reference projects by name without specifying spoke IDs. It can query any spoke via hub-local tools (proxied over SignalR WebSocket) to answer questions like "How are we looking?" or "What needs attention?" The hub NEVER directly connects to spoke MCPs.

### 3.2 The Spoke

Persistent daemon running on each of your work machines (Windows, macOS, or Linux). Each spoke:

- **Owns a local workspace** with project directories, skills, configuration, and a CLAUDE.md. Uses OS-appropriate paths: `~/.nexus/` on Linux/macOS, `%LOCALAPPDATA%\Nexus\` on Windows.
- **Publishes a Spoke Profile** — On registration, each spoke sends a profile block to the hub containing: display name, machine description, list of repos/projects it manages (with remote URLs), Jira instance URL + project keys, available integrations, and a free-text description. The spoke profile is configured in `config.yaml` and synced on registration.
- **Connects outbound to the hub** via persistent WebSocket (SignalR), reporting status and receiving commands.
- **Has full local access** to machine resources: Jira instances, Git repos, credentials, VPNs, file systems.
- **Runs a Claude Code instance per message** — the spoke agent is NOT a custom API client. It's a CC instance invoked with `--resume` and the spoke's session. The CC instance loads spoke-level and project-level skills and the spoke's CLAUDE.md, processes the message in context, and returns output. The CC process exits after each response; the next message resumes the same session.
- **Manages projects** — creates project folders from Jira tickets, pulls context, assembles implementation plans.
- **Orchestrates workers** — spins up Docker containers to execute specific coding tasks via Claude Code, each with access to spoke-level and project-level skills injected into the container. Workers are always Linux containers regardless of host OS (Docker Desktop on Windows/macOS, Docker Engine on Linux).
- **Maintains institutional knowledge** — skills directory and CLAUDE.md grow over time, capturing codebase conventions, decision rationale, and past outcomes.
- **Has local MCP servers (spoke-only)** — MCPs for local Jira, Git, and file system access. These MCPs are NEVER exposed to the hub. The spoke's CC instance uses these local MCPs via standard MCP config. The hub communicates with the spoke via SignalR only.

### 3.3 The Worker

Ephemeral Docker container launched by a spoke to execute a single coding task. Workers:

- **Run Claude Code CLI** with a constructed prompt built from ticket + repo + implementation plan + memory excerpts.
- **Are short-lived and focused** — one worker per job, scoped to a specific task (implement feature, write tests, refactor, investigate bug).
- **Stream output in real time** back through the spoke to the hub.
- **Are disposable** — container is destroyed on completion; artifacts (branches, diffs, logs, summaries) persist in the spoke's workspace.

### 3.4 Skills & Knowledge (CLAUDE.md + Skills Directory)

Each spoke workspace and every worker container has persistent knowledge through skills and CLAUDE.md:

**Spoke-Level Skills & CLAUDE.md** (`~/.nexus/`)
- `CLAUDE.md` at the root contains: spoke identity, machine info, repos/projects managed, conventions, and pointers to the skills directory
- `skills/` directory contains domain knowledge files that accumulate over time
- These are discovered and loaded natively by Claude Code — zero custom prompt assembly
- The spoke agent can self-manage knowledge via MCP tools: `update_skill(name, content)` and `create_skill(name, content)`

**Project-Level Skills & CLAUDE.md** (`projects/PROJ-4521/.nexus/`)
- Project-specific overrides and additions to spoke-level skills
- Override spoke defaults for this specific project
- Merged with spoke-level skills at worker launch time

**Worker Execution:**
- Workers get project-specific skills + spoke-level skills mounted, plus a generated `CLAUDE.md` per job
- Worker's CLAUDE.md contains: task description, ticket details, plan (if approved), and pointers to skill files
- This eliminates: custom prompt assembly pipeline, token budget management, truncation logic, memory summarization as a feature

**Hub Meta-Agent Skills:**
- The hub meta-agent also has skills that teach it how to manage the workspace, interpret spoke status, orchestrate work, and handle cross-spoke reasoning
- These skills live on the hub and are injected into each hub CC invocation

---

## 4. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                     HUB (MVP)                                         │
│              (Docker Compose on local machine)                        │
│                                                                      │
│  ┌─────────────────────┐        ┌──────────────────────────────┐   │
│  │  Next.js UI         │        │  .NET 10 API                 │   │
│  │  shadcn/ui          │◄──────►│  + SignalR Hub               │   │
│  │  Tailwind           │        │  + Hub-Local Tools           │   │
│  │                     │        │  (proxy over SignalR)        │   │
│  │ Desktop & Mobile    │        │                              │   │
│  └─────────────────────┘        │  PostgreSQL                  │   │
│                                 │  - Spoke registry            │   │
│                                 │  - Projects & jobs           │   │
│         │                       │  - Conversations & messages  │   │
│         │                       │  - Terminal streams          │   │
│         │                       │  - Pending actions (HITL)    │   │
│         │                       │                              │   │
│         │                  Hub CC Instance                     │   │
│         │                  (Meta-Agent)                        │   │
│         │                  + Hub-level skills                 │   │
│         │                  + Hub-local tools (SignalR proxy)  │   │
│         │                                                      │   │
│         │                       └──────────────────────────────┘   │
│         │                                  │                       │
└─────────┼──────────────────────────────────┼───────────────────────┘
          │                                  │
          │   WebSocket (outbound from spokes)
          │   No TLS required for MVP (LAN-only)
          │   Pre-shared auth token (optional for MVP)
   ┌──────┴────────────────────────────────────────────┐
   │                                                    │
   ▼                                                    ▼
┌──────────────────┐                         ┌──────────────────┐
│ SPOKE: Alpha     │                         │ SPOKE: Beta      │
│ (Machine on LAN) │                         │ (Machine on LAN) │
│                  │                         │                  │
│ .NET 10 Daemon   │                         │ .NET 10 Daemon   │
│                  │                         │                  │
│ CC Instance      │                         │ CC Instance      │
│ (per message)    │                         │ (per message)    │
│ + Spoke Skills   │                         │ + Spoke Skills   │
│ + Local MCPs     │                         │ + Local MCPs     │
│ (Jira, Git, etc) │                         │ (Jira, Git, etc) │
│                  │                         │                  │
│ Workspace:       │                         │ Workspace:       │
│ - projects/      │                         │ - projects/      │
│   - .nexus/      │                         │   - .nexus/      │
│     skills/      │                         │     skills/      │
│ - .nexus/        │                         │ - .nexus/        │
│   - skills/      │                         │   - skills/      │
│   - memories/    │                         │   - memories/    │
│                  │                         │                  │
│ Docker Workers   │                         │ Docker Workers   │
│ + Worker Skills  │                         │ + Worker Skills  │
│                  │                         │                  │
│ Local:           │                         │ Local:           │
│ - Jira           │                         │ - Jira           │
│ - Git Repos      │                         │ - Git Repos      │
│ - VPN access     │                         │ - VPN access     │
└──────────────────┘                         └──────────────────┘
```

### 4.1 Hub Technology Stack

- **API:** .NET 10 (ASP.NET Core Web API)
- **Real-Time Communication:** SignalR over WebSocket
- **Database:** PostgreSQL (Spoke registry, Projects, Jobs, Messages, Output streams)
- **Frontend:** Next.js 15+ with shadcn/ui and Tailwind CSS
- **Deployment (MVP):** Docker Compose on local machine (desktop, server, or laptop)
- **Deployment (Post-MVP):** Kubernetes cluster (self-hosted), reverse-proxied via Caddy
- **Authentication (MVP):** None — assumes trusted LAN. Post-MVP: Google OAuth (single-user only)

### 4.2 Spoke Technology Stack

- **Runtime:** .NET 10 Worker Service (long-running daemon, cross-platform)
- **Local Integrations:** Jira REST API, Git CLI, Docker SDK
- **AI Backend:** Claude Code CLI (spoke agent reasoning via CC instances + conversation)
- **Skills:** Directory-based skills injected into each CC invocation
- **Local MCP Servers:** Expose spoke-local capabilities (Jira, Git, file system) to the spoke's own CC instance only. Never exposed to hub.
- **Persistence:** File-based workspace (Markdown + JSON), OS-aware path resolution
- **Platform:** Windows (x64), macOS (arm64 and x64), Linux (x86-64)
- **Deployment:** Platform-specific: systemd service (Linux), Windows Service (Windows), launchd plist (macOS), or self-contained executable per platform

### 4.3 Worker Container

- **Base Image:** Ubuntu 24.04 LTS + Claude Code CLI
- **Runtime:** Claude Code CLI (no .NET runtime needed)
- **Skills:** Spoke-level and project-level skills mounted into container at launch
- **MCP Configuration:** The spoke passes an MCP config file to the worker container. Workers get access to the same local resources (Jira, Git, etc.) that the spoke machine has access to, via MCP servers configured by the spoke. The spoke pre-configures MCP access and mounts the config into each worker container. The hub never connects to spoke MCPs — it's a one-way outbound connection from spoke to hub, with bilateral communication over SignalR.
- **Input:** Project repository, injected markdown prompt (via CLAUDE.md), environment variables, skills directory, MCP config
- **Output:** Stdout/stderr streamed to spoke agent via Docker attach
- **Lifecycle:** Created per job, destroyed on completion
- **Session Management:** Each worker is a fresh CC invocation; no session continuity within a job

---

## 5. Communication Model

### 5.1 Topology: Outbound-Only WebSocket

Single communication pattern:

**Spoke → Hub (SignalR WebSocket):**
- Bidirectional, initiated by spoke (outbound only)
- Spoke maintains persistent WebSocket connection to hub
- Carries job assignments (hub → spoke), status updates, terminal output, conversation messages (both directions)
- Spokes behind firewalls/NATs can connect without inbound ports exposed
- Pre-shared token authentication
- Hub never initiates connections to spokes

**Hub → Spoke Queries (Via SignalR Proxy):**
- Hub CC meta-agent uses hub-local tools (not MCP) to query spoke state
- Hub-local tools send SignalR messages to spoke over existing WebSocket
- Spoke receives query, optionally invokes local CC or reads state, responds over same WebSocket
- Spoke MCPs (Jira, Git, file system) are LOCAL to spoke only, never exposed to hub

### 5.2 WebSocket Details

- **Transport:** WebSocket via SignalR
- **Encryption (MVP):** No TLS required (LAN-only, no public internet exposure)
- **Encryption (Post-MVP):** TLS 1.3 when exposed beyond LAN (via Tailscale, Cloudflare Tunnel, etc.)
- **Authentication (MVP):** Optional pre-shared bearer token (spokes register with hub). Hub validates token on `spoke.register`.
- **Authentication (Post-MVP):** Mandatory; JWT/OAuth for remote access
- **Resilience:** Spokes queue events locally on disconnection and replay on reconnect.
- **Heartbeat:** Spoke sends heartbeat every 30s. Hub responds. Timeout = no heartbeat for 2 minutes → spoke marked offline.

### 5.3 Hub-Local Tools (Spoke Queries)

Hub CC meta-agent uses hub-local tools to query spokes. These tools do NOT connect to spoke MCPs. Instead, they send SignalR messages over the existing WebSocket and wait for responses.

**Hub-Local Tools:**
- `query_spoke_status(spoke_id)` — Returns cached state from spoke daemon immediately (active jobs, projects, resource usage, etc.). No CC involved. Sub-second latency.
- `query_spoke(spoke_id, question)` — Sends a query via SignalR to the spoke. The spoke service spins up a CC container to reason about the query, then sends the response back via SignalR. Hub MCP tool returns the response to hub CC. 5-15 second latency. Hub CC decides which tool to use based on query nature.
- `list_spokes()` — All connected spokes with status (reads from hub database)
- `get_cross_spoke_summary()` — Aggregated status across all spokes (reads from hub database)
- `list_all_pending_actions()` — All HITL items across all spokes (reads from hub database)
- `search_projects(query)` — Search projects across all spokes (reads from hub database)
- `get_timeline()` — Recent activity across system (reads from hub database)

Most hub tools query the hub's own PostgreSQL database. `query_spoke_status()` returns spoke daemon state immediately; `query_spoke()` spins up a spoke CC instance to reason about the query. Both communicate over SignalR WebSocket, not via MCP.

### 5.4 Spoke-Local MCPs (LOCAL ONLY)

Spokes have MCP servers for LOCAL use only:

**Spoke MCPs (used by spoke's CC instance):**
- Jira MCP — Query local Jira instance
- Git MCP — Query local Git repos
- File system MCP — File access

These MCPs are configured in `./spoke-mcp-config.json` on the spoke and are NEVER exposed to the hub or any external system. Only the spoke's own CC instance can access them.

### 5.5 Event Taxonomy

**Spoke → Hub (Telemetry & Status)**

| Event | Payload |
|---|---|
| `spoke.register` | Identity (name, ID), capabilities (Jira, Git), config |
| `spoke.heartbeat` | Status, active job count, resource usage |
| `project.created` | Project metadata (key, name, status, ticket link) |
| `project.updated` | Updated status, metadata |
| `job.created` | Job metadata (type, project key, expected duration) |
| `job.status_changed` | New status (Queued, Running, Completed, Failed), summary |
| `job.output` | Terminal output chunk (streaming, timestamp) |
| `message.from_spoke` | Conversational response from spoke agent to user |

**Hub → Spoke (Commands & Directives)**

| Command | Payload |
|---|---|
| `job.assign` | Project key, job type (implement/test/refactor), parameters |
| `job.approve` | Job ID, optional modifications to prompt or plan |
| `job.cancel` | Job ID |
| `message.to_spoke` | Conversational message from user |
| `spoke.configure` | Updated config (approval modes, concurrency limits, etc.) |
| `spoke.query` | Query from hub meta-agent (correlationId, query, context) |

**Spoke → Hub (Query Responses)**

| Event | Payload |
|---|---|
| `spoke.query_response` | Response to hub query (correlationId, response, metadata) |

### 5.6 Protocol

All messages are JSON over WebSocket with metadata:

```json
{
  "event_type": "job.status_changed",
  "timestamp": "2026-04-04T10:30:00Z",
  "correlation_id": "uuid",
  "payload": { /* event-specific data */ }
}
```

Hub acknowledges all spoke events. Spokes implement exponential backoff for reconnection (1s → 32s cap).

---

## 6. Data Model

### 6.1 Core Entities (PostgreSQL)

**Spoke**
```
- id (UUID, PK)
- name (string, e.g., "Alpha Machine")
- secret_hash (bcrypt of pre-shared token)
- status (enum: online, offline, error)
- last_seen (timestamp)
- capabilities (JSON: { jira: true, git: true, ... })
- config (JSON: approval modes, concurrency, max_turns, timeout, etc.)
- profile (JSON: display name, machine description, repos/projects, Jira URL + keys, integrations, free-text description)
- created_at, updated_at (timestamps)
```

**Project**
```
- id (UUID, PK)
- spoke_id (UUID, FK → Spoke)
- external_key (string, e.g., "PROJ-4521")
- name (string)
- status (enum: planning, active, paused, completed, failed)
- ticket_url (string, optional)
- created_at, updated_at (timestamps)
```

**Job**
```
- id (UUID, PK)
- idempotency_key (string, optional, unique) — For deduplication on create operations
- project_id (UUID, FK → Project)
- spoke_id (UUID, FK → Spoke)
- type (enum: implement, test, refactor, investigate, custom)
- status (enum: Queued, AwaitingApproval, Running, Completed, Failed, Cancelled)
- summary (text, agent-generated outcome)
- started_at, completed_at (timestamps, nullable)
- created_at (timestamp)
```

**Conversation**
```
- id (UUID, PK)
- spoke_id (UUID, FK → Spoke, nullable) — null for hub-level conversations
- title (string)
- cc_session_id (string) — Session ID on spoke or hub CC instance
- created_at, updated_at (timestamps)
```

**ConversationMessage**
```
- id (UUID, PK)
- conversation_id (UUID, FK → Conversation)
- role (enum: user, assistant, system)
- content (text)
- timestamp
```

**Message** (legacy; kept for compatibility)
```
- id (UUID, PK)
- spoke_id (UUID, FK → Spoke)
- role (enum: user, spoke_agent, system)
- content (text)
- job_id (UUID, FK → Job, nullable)
- timestamp
```

**OutputChunk** (for streaming terminal output)
```
- id (UUID, PK)
- job_id (UUID, FK → Job)
- sequence (int)
- content (text)
- timestamp
```

**PendingAction** (HITL queue item)
```
- id (UUID, PK)
- spoke_id (UUID, FK → Spoke)
- project_id (UUID, FK → Project, nullable)
- gate_type (enum: plan_review, pre_execution, post_execution, spoke_question, pr_review, custom)
- summary (string)
- description (text)
- metadata (JSONB) — Task-specific data
- status (enum: pending, resolved)
- resolved_at (timestamp, nullable)
- created_at (timestamp)
```

### 6.2 Spoke Workspace (File-Based)

```
~/.nexus/
├── CLAUDE.md                    # Spoke identity, machine info, repos/projects, conventions, skills pointers
├── config.yaml                  # Hub URL, auth token, spoke ID, capabilities, profile, max_turns, timeout
├── cc-session-id                # CC session ID for spoke agent (persistent)
├── skills/                      # Spoke-level skills (discovered natively by CC)
│   ├── domain-1.md              # Domain knowledge files
│   ├── domain-2.md
│   └── ...
├── agent-state/
│   ├── conversation.log         # Chat history (JSON lines)
│   └── planner-state.json       # Queue, pending approvals, priorities
├── mcp/
│   ├── config.yaml              # MCP server config (Jira, Git, file system)
│   └── log/                     # MCP request/response logs
│
├── projects/
│   ├── PROJ-4521/
│   │   ├── CLAUDE.md            # Project-specific task description, ticket details, plan pointers
│   │   ├── .nexus/
│   │   │   ├── skills/          # Project-level skills (override spoke skills)
│   │   │   │   ├── patterns.md
│   │   │   │   └── db-schema.md
│   │   ├── .meta/
│   │   │   ├── ticket.json      # Cached Jira ticket
│   │   │   ├── plan.md          # Implementation plan (agent-generated)
│   │   │   └── status.json      # Current status, blockers, notes
│   │   ├── repo/                # Cloned working copy
│   │   └── jobs/
│   │       ├── job-001/
│   │       │   ├── prompt.md    # Full prompt sent to worker
│   │       │   ├── output.log   # Terminal output stream
│   │       │   ├── summary.md   # Agent summary of outcome
│   │       │   └── status.json  # Job status, metrics
│   │       └── job-002/...
│   └── PROJ-4587/...
│
└── templates/
    ├── worker-prompt-base.md    # Base prompt for all workers
    └── plan-template.md         # Template for implementation plans
```

---

## 7. User Experience Summary

### 7.1 Core Flows

**Morning Check (Mobile)**
- Open hub dashboard on phone.
- See status of all spokes: which are online, recent activity, jobs completed overnight.
- Tap into a spoke and ask: "What finished?" Spoke provides summary and links.
- Review plan if needed, approve or request changes.

**Assign Work (Hub UI)**
- Navigate to a spoke or project.
- Create a new job manually, or tell spoke to "work through sprint backlog."
- Optionally set approval gate (plan review, full autonomy).
- Job enters queue. Spoke spins up worker when its turn arrives.

**Monitor Execution (Desktop)**
- Stream live terminal output from active worker.
- Pause, cancel, or adjust parameters mid-execution if needed.
- On completion, review summary and outcome artifacts (branches, diff, log).

**Direct Spoke Conversation**
- Ask spoke questions: "What's the status of PROJ-4521?" "Is there overlap between these two tickets?"
- Spoke reasons about local state and memory to respond.

### 7.2 Hub UI Views (Next.js)

**Hub Conversation (Meta-Agent)**
- Chat interface with the hub CC meta-agent.
- Ask cross-system questions: "How are we looking?", "What's blocking PROJ-4521?", "Which spokes are busy?"
- Meta-agent queries spokes via MCP and synthesizes answers.
- Conversation history persisted in hub database.
- Separate from spoke conversations; shows hub-level reasoning.

**Awaiting Input** (Unified Queue)
- Cross-spoke view showing all items waiting for human-in-the-loop (HITL) attention in one place.
- Prioritized queue sorted by age (oldest first); sortable by priority/type.
- Each item shows: spoke badge, project/ticket reference, gate type pill, time waiting, description, quick-action buttons.
- Primary mobile view; prominent tab/sidebar link on desktop with badge count.
- Gateway to all HITL gates: plan review, pre-execution, post-execution, spoke questions, PR review comments.

**Dashboard**
- Grid of all spokes with online/offline status, active job count, recent activity.
- Feed of latest events across all spokes.
- Badge link to Awaiting Input queue for quick access to pending items.
- Link to Hub Conversation for asking meta-agent questions.

**Spoke Detail**
- Conversational interface (chat window) — conversation with the spoke's CC agent.
- Multiple conversation threads selectable from sidebar (supports threading).
- Project list with status.
- Active/queued/completed jobs for this spoke.

**Project Detail**
- Ticket metadata (title, description, acceptance criteria).
- Current implementation plan (editable).
- Job history with links to output logs and summaries.
- Blockers and notes.

**Job Stream**
- Live terminal output (read-only, scrollable, searchable).
- Status bar with job metadata (start time, elapsed, current step).
- Controls: pause, cancel.

**Timeline**
- Chronological feed of all activity across all spokes.
- Searchable by spoke, project, or text.

---

## 8. Approval & Autonomy Model

Approval gates are between **phases of a job**, not on individual Claude Code tool calls. CC instances run with bypass permissions and are never interrupted mid-execution.

Configurable approval gates scope by spoke, project, or job:

- **Plan Review (Gate: Before Coding Starts)** — Spoke generates implementation plan in a planning-phase CC instance; you review and approve before the implementation phase begins and the worker spins up.
- **Pre-Execution Approval (Gate: Before Worker Spins Up)** — Job is queued and ready to run; waiting for final go-ahead before worker container launches.
- **Post-Execution Review (Gate: After Worker Completes, Before PR)** — Job completed; worker summary, branch, or PR waiting for your review before merge/close.
- **Spoke Question** — Spoke encountered a blocker or ambiguity and needs user guidance.
- **PR Review Comments** — PR has unresolved comments routed to user for decision or confirmation.
- **Full Autonomy** — Spoke works through backlog and only pings you on failures or unresolved questions. All gates are skipped.

The spoke orchestrator checks gates between workflow steps. CC itself is never interrupted — the gate is enforced by the spoke daemon before launching the next phase.

All items awaiting HITL attention flow to the **Awaiting Input** unified queue for single-point visibility.

Config stored in spoke's `config.yaml` with sensible defaults. Override per-job at creation time.

---

## 9. Spoke Intelligence & Memory

### 9.1 Spoke Capabilities

- **Conversational (CC-Powered)** — Each message invokes a CC instance with `--resume` and the spoke's session ID. Responds to natural language, understands projects, status, blockers, and priorities. Full conversation context available via session.
- **Context-Aware** — Has access to local Jira, Git, file system, skills directory, and CLAUDE.md.
- **Skill-Driven Behavior** — Spoke-level and project-level skills guide behavior across all CC invocations.
- **Self-Directing** — Can proactively suggest work, flag blockers, identify related tickets.
- **Plan Generation** — Given a ticket, produces a structured implementation plan (scope, approach, tests, risks).
- **Knowledge Accumulation** — Learns from past jobs and decisions. Updates skills and CLAUDE.md with new patterns, gotchas, and rationale. Spoke agent has MCP tools: `update_skill(name, content)` and `create_skill(name, content)` for self-managed knowledge.
- **Local MCPs** — Uses local MCPs (Jira, Git, file system) via standard MCP config. These are strictly local; never exposed to hub.
- **SignalR Query Handler** — Receives queries from hub meta-agent over SignalR WebSocket. Can invoke local CC or return state directly.
- **PR Monitoring & Comment Routing (GitHub & GitLab)** — Monitors open pull requests on a configurable interval (~15 minutes). Routes PR comments to the hub as a list of proposed comments for user review before posting. Tracks which PR comments have been processed to avoid duplicates and handles threaded conversations correctly. Auto-creates fix jobs for actionable feedback, responds to comments when appropriate, and escalates ambiguous feedback to the user's Awaiting Input queue. Supports GitHub and GitLab via a service interface pattern with two implementations.

### 9.2 Skills & Knowledge (CLAUDE.md as Native Discovery)

Each spoke workspace uses a skill-based approach that eliminates custom prompt assembly:

**Spoke-Level Knowledge** (`~/.nexus/`)
- `CLAUDE.md` at the root: Spoke identity, machine info, repos/projects managed, conventions, and pointers to the skills directory
- `skills/` directory: Domain knowledge files that accumulate over time (cross-project patterns, gotchas, conventions, etc.)
- CC discovers these natively when loaded — no custom prompt assembly needed
- Spoke agent can self-manage knowledge via MCP tools: `update_skill()` and `create_skill()`

**Project-Level Knowledge** (`projects/*/`)
- `CLAUDE.md` per project: Task description, ticket details, plan (if approved), and pointers to skill files
- `skills/` directory: Project-specific patterns, architecture decisions, naming conventions, test structure
- Merged with spoke-level skills at worker launch time

**Worker Execution:**
- Workers load spoke-level skills + project-level skills
- A generated `CLAUDE.md` is provided per job with: task description, ticket details, plan, skill pointers
- CC discovers and uses these natively — eliminates token budgeting, truncation, manual summarization
- Full prompt (skills + CLAUDE.md) is saved to `projects/<key>/jobs/job-###/prompt.md` for auditability

**Benefits:**
- No token budget management or summarization overhead
- Persistent, discoverable knowledge grows naturally
- Spoke and workers use identical knowledge access patterns
- Skill updates instantly available to all future CC invocations

---

## 10. Constraints & Assumptions

### Constraints

- **Single-User Only** — Hub is exclusively for you. No multi-tenancy, no shared access, no invite system.
- **Trusted LAN (MVP)** — Hub and spokes assumed to be on the same physical LAN. No authentication required for MVP. Tailscale/Cloudflare Tunnel/VPN required for remote access (post-MVP).
- **No Public Internet Exposure (MVP)** — Hub is not accessible from the public internet. Spokes connect to hub via local network (e.g., `http://192.168.1.100:5000` or `http://nexus-hub.local:5000`).
- **Credential Isolation** — Hub has zero knowledge of external system credentials (Jira, GitHub, Azure, etc.). All credentials live on spokes.
- **No Source Code in Hub** — Source code never flows to the hub. Only metadata, summaries, and terminal output.
- **Cross-Platform Spokes** — Spokes run on Windows, macOS, and Linux. Hub remains Linux-only for MVP (Docker Compose).
- **Docker Requirement on Spokes** — Each spoke must have Docker available (Docker Desktop on Windows/macOS, Docker Engine on Linux) for worker container execution. Workers themselves are always Linux containers.
- **Synchronous Worker Execution** — Workers run sequentially per spoke. Concurrency is managed by spoke queue; no parallel workers on the same spoke (simplifies resource management).
- **Configurable Timeouts** — `max_turns` and `timeout` are configurable per spoke and per job in spoke config. Hub can also set defaults that spokes inherit.

### Assumptions

- **Reliable LAN Connectivity** — Spokes have reliable connectivity to the hub over LAN. Local reconnection logic handles brief outages.
- **Docker Available on Spokes** — Each spoke runs Docker for worker containers.
- **Claude API Key on Spoke** — Each spoke has a valid Claude API key for spoke agent reasoning and worker execution.
- **Jira REST API** — Phase 1 uses REST API. MCP integration is planned for post-MVP.
- **Git Repos are Cloneable Locally** — Each spoke can access and clone the repos it manages (SSH keys, credentials, VPN access already set up).
- **Git Platform Support** — System supports GitHub and GitLab for PR monitoring and integration via a service interface pattern with implementations for each platform.
- **Fast Development Velocity** — Build with Claude Code, moving at ~1 week per phase. Scope is tight and realistic.

---

## 11. Implementation Phases

### Phase 1: Foundation (Week 1)

**Goal:** Hub ↔ spoke communication and basic dashboard. MVP: no authentication, LAN-only.

- **Hub:** .NET 10 API with PostgreSQL, SignalR endpoint, spoke registration and heartbeat logic. **No OAuth for MVP** — auth is post-MVP.
- **Spoke:** .NET 10 worker service, config file (`config.yaml`), outbound WebSocket connection to hub over LAN, heartbeat and status reporting.
- **Hub UI:** Minimal Next.js dashboard showing connected spokes, online/offline status, basic activity feed.
- **Deployment:** Hub runs via Docker Compose on local machine. Spokes connect via local IP address (e.g., `http://192.168.1.100:5000`).
- **No AI, no Docker, no job execution yet. No authentication.** Just communication backbone and persistence.

**Deliverables:** Working end-to-end spoke registration, heartbeat, and hub dashboard. Hub accessible from local network.

### Phase 2: Workspace & Conversation (Week 2)

**Goal:** Spokes manage workspaces and have basic conversational intelligence.

- **Spoke:** Local workspace initialization, project folder creation from Jira tickets, Jira REST API integration.
- **Spoke → Hub:** Project creation events, project metadata syncing.
- **Hub UI:** Project list per spoke, project detail view with ticket metadata.
- **Spoke Agent:** Basic conversational interface — spoke uses Claude API to respond to queries about local projects and status.
- **Message Log:** Store and display conversation history in hub.

**Deliverables:** Spoke can pull Jira tickets, create projects, answer questions about its local state via conversation.

### Phase 3: Workers & Job Execution (Week 3)

**Goal:** Spokes spin up Claude Code workers to execute tasks.

- **Spoke:** Docker integration — pull/build worker image, launch container with mounted repo and prompt, stream output.
- **Spoke:** Terminal output streaming to hub in real time.
- **Hub UI:** Job creation form, live terminal output view, job status and history.
- **Worker Container:** Ubuntu + Claude Code CLI, prompt injection, output streaming.
- **Approval Gates:** Simple implementation — job can be marked "awaiting-approval" before worker spins up.

**Deliverables:** End-to-end job execution: create job → approve → worker spins up → output streams → job completes.

### Phase 4: Intelligence & Autonomy (Week 4)

**Goal:** Spoke becomes truly autonomous and useful.

- **Spoke:** Memory system — read/write/append to memory files (global, codebase notes, decision log).
- **Spoke:** Plan generation — given a ticket, produce an implementation plan with scope, approach, tests, risks.
- **Spoke:** Self-directed work — "work through sprint backlog" directive spins up multiple jobs with user approval.
- **Spoke:** Post-job processing — summarize outcomes, extract learnings, update memory.
- **Hub UI:** Plan review interface, approval gates UI (plan vs. full autonomy), batch job management.

**Deliverables:** Spoke can autonomously work through a backlog with user approval at key gates. Memory system captures learnings.

### Phase 5: Polish & Hardening (Week 5–6)

**Goal:** Production-ready, documented, resilient MVP. Post-MVP roadmap for auth and remote access.

- **Hub UI:** Responsive design, mobile optimization, advanced search, timeline view, metrics.
- **Spoke:** Error handling, retry logic, graceful degradation.
- **Deployment:** Docker Compose for hub (MVP), installer script for spoke (all platforms).
- **Documentation:** Setup guide, configuration reference (LAN-only), troubleshooting, UX guide.
- **Testing:** Integration tests, failure scenarios, load testing for message streams.
- **Post-MVP Roadmap:** Document auth strategy (Google OAuth), remote access (Tailscale/Cloudflare), TLS setup, Kubernetes manifests.

**Deliverables:** Fully functional, documented, deployable MVP ready for daily use on trusted LAN.

---

## 12. Future Considerations

### Short-Term (Post-Launch)

- **Enhanced Spoke Integrations** — Linear, GitHub Issues, Azure DevOps in addition to Jira.
- **Plan Review UI** — Dedicated interface for approving generated implementation plans before worker execution.
- **Memory Summarization** — Automated background summarization of memory files to keep them focused and useful.
- **Spoke-to-Spoke Collaboration** — Coordination between spokes on cross-cutting concerns (e.g., API contract changes affecting multiple repos).

### Medium-Term

- **Code Review Integration** — Worker output submitted to CodeRabbit for automated review before PR creation.
- **Metrics & Analytics** — Track agent productivity, success rates, common failure modes. Dashboard with trends.
- **Windows / macOS Support** — Expand platform support beyond Linux.
- **Alternative Deployment Platforms** — Support for other self-hosted deployment options.

### Long-Term

- **Voice Interface** — Talk to spokes via the hub mobile app.
- **Multi-User / Team Mode** — Extend from single-user to small teams (different spokes, shared project visibility).
- **Self-Improving Agents** — Spokes that analyze their own success/failure patterns and refine their templates and memory strategies.

---

## 13. Non-Functional Requirements

### Performance

- **Hub Response Time** — Dashboard and API responses < 200ms (p99).
- **Message Delivery** — Spoke → hub messages delivered within 100ms under normal network conditions.
- **Output Streaming** — Terminal output streamed with < 500ms latency.

### Reliability

- **Spoke Reconnection** — Automatic reconnect with exponential backoff. No message loss on disconnect (local queue).
- **Hub Availability** — Target 99.5% uptime. Graceful degradation if database is unavailable (spokes continue offline).
- **Job Idempotency** — Jobs can be safely rerun. Worker output is idempotent (no duplicated effects).

### Security

- **Authentication** — Google OAuth for hub access (single user). Pre-shared tokens for spoke ↔ hub.
- **Encryption** — TLS 1.3 for all hub ↔ spoke traffic. At-rest encryption of workspace via OS (optional).
- **Audit Trail** — All hub commands and spoke events logged with timestamps and user context.
- **Network Isolation** — Hub not accessible from public internet. VPN/Tailscale/Tunnel required.

### Observability

- **Logging** — Structured logs (JSON) for all hub and spoke events. Searchable via hub.
- **Tracing** — Correlation IDs on all events for end-to-end request tracing.
- **Alerting** — Optional: spoke offline > 5 minutes, job failure, hub errors.

### Testing

- **Unit Tests** — All components include unit tests as part of development, not as separate stories. Hub API endpoints, spoke daemon logic, worker orchestration, and data models all have test coverage.

---

## 14. Success Criteria

- [ ] End-to-end job execution: create ticket-based project → approve plan → worker executes → output streams → job completes.
- [ ] Conversational spoke agent that answers questions about local projects and status.
- [ ] Memory system that captures codebase conventions and decision rationale.
- [ ] Mobile-optimized hub dashboard showing all spokes and recent activity.
- [ ] Automatic spoke reconnection after network outage (no message loss).
- [ ] Worker can autonomously work through a multi-item backlog with user approval gates.

---

## 15. Out of Scope (Phase 1)

- Multi-user access or shared workspaces.
- Plugin system or extensibility framework.
- GitHub / Linear / Azure DevOps integration (Jira only).
- Advanced code review workflows or automatic PR merging.
- Voice interface or advanced analytics.
- Public open source release (private repository during Phase 1–4).
