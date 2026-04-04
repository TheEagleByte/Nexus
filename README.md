# Nexus

Self-hosted hub-and-spoke platform for orchestrating persistent AI coding agents across multiple machines. Run autonomous Claude Code workers on every dev machine you own, controlled from a single dashboard.

## What It Does

Nexus gives you persistent, intelligent coding agents on each machine that remember codebase conventions, maintain institutional knowledge, and coordinate work across all your projects — controllable from a single dashboard on any device.

- **Hub** — Central command center (Docker Compose) with a real-time dashboard, conversational meta-agent, job orchestration, and message history. Desktop and mobile responsive.
- **Spokes** — Persistent daemons on each work machine (Windows, macOS, Linux). Each spoke owns its local workspace, credentials, Git repos, Jira access, and runs Claude Code instances per message. Connects outbound to the hub via SignalR WebSocket.
- **Workers** — Ephemeral Docker containers launched by spokes to execute individual coding tasks via Claude Code CLI. One worker per job, destroyed on completion.

## Architecture

```text
                        ┌─────────────────────────┐
                        │          HUB            │
                        │   .NET 10 API + SignalR │
                        │   Next.js Dashboard     │
                        │   PostgreSQL            │
                        │   Hub Meta-Agent (CC)   │
                        └──────────┬──────────────┘
                                   │
                    WebSocket (outbound from spokes)
                                   │
                 ┌─────────────────┼───────────────────┐
                 │                                     │
          ┌──────┴───────┐                     ┌───────┴──────┐
          │  SPOKE Alpha │                     │  SPOKE Beta  │
          │  .NET 10     │                     │  .NET 10     │
          │  CC Instance │                     │  CC Instance │
          │  Local MCPs  │                     │  Local MCPs  │
          │  Docker      │                     │  Docker      │
          │  Workers     │                     │  Workers     │
          └──────────────┘                     └──────────────┘
```

**Key design constraints:**

- Single-user, trusted LAN operation. No multi-tenancy.
- No credentials or source code leave the spoke. Only metadata, status, summaries, and terminal output flow to the hub.
- Spokes connect outbound only — no inbound firewall holes required.
- Hub never connects to spoke MCPs directly; all communication goes over SignalR.

## Tech Stack

| Component          | Technology                                               |
| ------------------ | -------------------------------------------------------- |
| Hub API            | .NET 10 (ASP.NET Core)                                   |
| Hub Real-Time      | SignalR (WebSocket)                                      |
| Hub Database       | PostgreSQL                                               |
| Hub UI             | Next.js 15+, shadcn/ui, Tailwind CSS                     |
| Hub Deployment     | Docker Compose (MVP), Kubernetes (post-MVP)              |
| Spoke Runtime      | .NET 10 Worker Service (cross-platform daemon)           |
| Spoke Integrations | Jira REST API, Git CLI, Docker SDK, local MCP servers    |
| Spoke AI           | Claude Code CLI (per-message invocation with `--resume`) |
| Workers            | Ubuntu 24.04 LTS Docker containers + Claude Code CLI     |
| Auth (MVP)         | None (trusted LAN)                                       |
| Auth (Post-MVP)    | Google OAuth (hub UI), mTLS/JWT (spoke-to-hub)           |

## Design System

Dark, terminal-inspired UI optimized for engineers. Cyan accents, monospace data display, high-contrast WCAG AA compliant. Mobile-first with desktop power-user features.

## Project Structure

```text
Nexus.Hub/
├── Nexus.Hub.Api/              # ASP.NET Core Web API + SignalR hub
├── Nexus.Hub.Api.Tests/        # API unit tests
├── Nexus.Hub.Domain/           # Domain models, interfaces, events
├── Nexus.Hub.Domain.Tests/     # Domain unit tests
└── Nexus.Hub.Infrastructure/   # EF Core, repositories, services

Nexus.Spoke/
├── Nexus.Spoke.Agent/          # .NET Worker Service daemon
├── Nexus.Spoke.Agent.Tests/    # Agent unit tests
└── Nexus.Spoke.Domain/         # Spoke domain models

nexus-ui/                       # Next.js 15+ dashboard
```

## Skills & Knowledge System

Nexus uses Claude Code's native skills and CLAUDE.md system for persistent knowledge — no custom prompt assembly pipeline.

- **Spoke-level** (`~/.nexus/skills/`, `~/.nexus/CLAUDE.md`) — Machine-wide conventions, domain knowledge
- **Project-level** (`projects/<id>/.nexus/`) — Project-specific overrides and context
- **Worker** — Gets merged spoke + project skills mounted into each container, plus a generated CLAUDE.md per job

Skills accumulate over time as agents learn codebase conventions, decision rationale, and past outcomes.

## Security Model

- MVP assumes trusted LAN, no authentication required
- Credentials are compartmentalized at the spoke level — hub has zero knowledge of external system credentials
- All spoke connections are outbound-only (WebSocket)
- Post-MVP adds Google OAuth, mTLS, TLS 1.3, and remote access via Tailscale/Cloudflare Tunnel
- Full security model documented in [`docs/security-model.md`](docs/security-model.md)

## Documentation

| Document                                               | Description                                                           |
| ------------------------------------------------------ | --------------------------------------------------------------------- |
| [`docs/design-document.md`](docs/design-document.md)   | Core design, goals, architecture, communication model                 |
| [`docs/technical-design.md`](docs/technical-design.md) | Implementation-ready technical specs, project structure, code samples |
| [`docs/api-contract.md`](docs/api-contract.md)         | Full REST + SignalR API contract                                      |
| [`docs/security-model.md`](docs/security-model.md)     | Threat model, trust boundaries, credential handling                   |
| [`docs/ux-design.md`](docs/ux-design.md)               | UI design system, color palette, component specs                      |
| [`docs/jira-breakdown.md`](docs/jira-breakdown.md)     | Epic/story breakdown for implementation                               |

## License

See [LICENSE](LICENSE) for details.
