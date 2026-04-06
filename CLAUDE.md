# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Nexus is a self-hosted hub-and-spoke platform for orchestrating persistent AI coding agents (Claude Code) across multiple machines. A central **Hub** manages jobs and state; **Spokes** are daemons on work machines that receive jobs and launch ephemeral Docker **Workers** running Claude Code CLI.

## Architecture

- **Hub** — .NET 10 ASP.NET Core API + PostgreSQL + SignalR WebSocket. Layered: Domain (entities/interfaces) → Infrastructure (EF Core/repos/services) → Api (controllers/hubs/middleware).
- **Spoke** — .NET 10 Worker Service. Connects outbound to Hub via SignalR. Runs Claude Code CLI, manages local workspaces, Jira/Git/Docker integration. Config in `src/Nexus.Spoke/config.yaml`.
- **Dashboard** — Next.js 16.2.2 + React 19 + shadcn/ui + Tailwind CSS 4. SignalR client for real-time updates. Lives in `web/`.
- **Workers** — Ephemeral Docker containers (Ubuntu 24.04 + Claude Code CLI), launched by spokes per-task.

Communication: Spoke initiates outbound WebSocket to Hub (no inbound ports needed). Credentials stay on spoke; Hub has zero knowledge of them.

## Build & Run

Prerequisites: .NET 10 SDK, Node.js 18+, Docker

```bash
# Database
docker compose up -d                    # PostgreSQL 17 on port 5433

# Hub API (http://localhost:5000)
cd src && dotnet run --project Nexus.Hub.Api

# Spoke daemon
cd src && dotnet run --project Nexus.Spoke

# Dashboard (http://localhost:3000)
cd web && npm install && npm run dev
```

## Test

```bash
# Hub tests (80% line coverage enforced in CI)
cd src && dotnet test Nexus.Hub.sln --configuration Release

# Spoke tests
cd src && dotnet test Nexus.Spoke.sln

# Run a single test
cd src && dotnet test Nexus.Hub.Api.Tests --filter "FullyQualifiedName~TestMethodName"

# Frontend lint
cd web && npm run lint
```

CI coverage excludes: Migrations, Program.cs, HubModels.cs, EF entities, repositories, API models.

## Solutions

Two separate .sln files in `src/`:
- `Nexus.Hub.sln` — Hub.Domain, Hub.Infrastructure, Hub.Api, Hub.Api.Tests
- `Nexus.Spoke.sln` — Spoke, Spoke.Tests

## Key Services

**Hub:** `ISpokeService` (registration/status), `IJobService` (job lifecycle), `IProjectService` (CRUD), `IMessageService` (conversation history). SignalR hub at `NexusHub`.

**Spoke:** `HubConnectionWorker` (WebSocket lifecycle), `CommandQueueWorker` (job dispatch), `HeartbeatWorker` (keepalive), `WorkspaceInitializer` (runs first on startup). Startup order matters: WorkspaceInitializer → HubConnection → CommandQueue.

## Dashboard (Next.js 16)

**Read `web/AGENTS.md` before modifying frontend code.** Next.js 16 has breaking changes from what you know. Check `node_modules/next/dist/docs/` for current API docs.

## Design Decisions

- MVP: trusted LAN, no auth. Post-MVP: Google OAuth + mTLS.
- Spoke skills stored at `~/.nexus/skills/` (or `%LOCALAPPDATA%\Nexus\`), project-level at `projects/<id>/.nexus/`.
- Workers get merged spoke + project skills/CLAUDE.md injected at launch.

## Docs

- `docs/design-document.md` — goals, architecture overview
- `docs/technical-design.md` — implementation details
- `docs/api-contract.md` — REST + SignalR API specs
- `docs/security-model.md` — threat model
- `docs/ux-design.md` — UI design
