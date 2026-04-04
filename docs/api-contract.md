# Nexus API Contract Specification

**Status:** Implementation-Ready
**Last Updated:** 2026-04-04
**Version:** 1.0
**Stack:** .NET 10 Hub API + SignalR, PostgreSQL, Next.js Frontend

---

## 1. Overview

Nexus exposes two communication layers:

1. **REST API** — Stateless CRUD operations for projects, jobs, spokes, messages, and timeline. Used by the Next.js frontend (as BFF) and spoke administrative operations.
2. **SignalR WebSocket Hub** — Persistent bidirectional channel between hub and spokes. Spoke agents receive assignments and send status/output; hub broadcasts events and commands to connected spokes and frontend clients.

### 1.0 Architecture Notes

**Backend-for-Frontend (BFF):** The Next.js frontend acts as a BFF and calls the .NET API server-side only. No direct browser-to-API calls; CORS is not needed for the primary frontend. CORS configuration only applies to direct API consumers (future administrative tools, integrations, etc.).

### 1.1 Authentication

- **Frontend (Google OAuth 2.0 → JWT)**: Users authenticate via Google OAuth. Hub exchanges OAuth token for JWT (15-minute lifetime). Refresh token stored in httpOnly cookie (7-day lifetime). Auto-refresh endpoint available.
- **Spoke (PSK → JWT)**: Spoke registers with pre-shared key (PSK). Hub returns spoke JWT (24-hour lifetime). Spoke uses JWT for all subsequent SignalR connections. PSK sent only once per 24-hour cycle; auto-refresh refreshes JWT without PSK re-submission.
- **REST API**: All endpoints (except `/api/auth/google` and `/api/spokes/register`) require `Authorization: Bearer <jwt_token>` header.

### 1.2 Base URLs & Versioning

```
Hub API:        https://<hub-host>/api/v1
SignalR Hub:    wss://<hub-host>/hubs/nexus

API Versioning: Semantic. No breaking changes within v1; v2 released when breaking changes required.
```

### 1.3 Error Response Format

All error responses use this standard format:

```json
{
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "Spoke with id 'abc123' not found",
    "status": 404,
    "timestamp": "2026-04-04T14:30:00Z",
    "correlationId": "req-uuid-here",
    "details": {
      "spokeId": "abc123"
    }
  }
}
```

**Common error codes:**
- `INVALID_REQUEST` (400) — Malformed request body or query params
- `UNAUTHORIZED` (401) — Missing or invalid auth token
- `FORBIDDEN` (403) — User lacks permission for this resource
- `RESOURCE_NOT_FOUND` (404) — Resource doesn't exist
- `CONFLICT` (409) — Job already in terminal state, spoke already registered, etc.
- `INTERNAL_ERROR` (500) — Unexpected server error
- `SERVICE_UNAVAILABLE` (503) — Database or external service unreachable

---

## 2. REST API Endpoints

### 2.1 Authentication

#### POST `/api/auth/google`
Exchange Google OAuth token for Nexus JWT.

**Request:**
```json
{
  "googleToken": "ya29.a0AfH6SMBx..."
}
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 900,
  "user": {
    "id": "user-uuid-here",
    "email": "user@example.com",
    "name": "Project User"
  }
}
```

**Notes:**
- JWT lifetime: 15 minutes (900 seconds)
- Refresh token stored in httpOnly cookie (7-day lifetime)
- Use POST `/api/auth/refresh` to refresh expired JWT

**Status Codes:**
- `200` — Token exchanged successfully
- `400` — Invalid Google token
- `401` — Google token verification failed

---

#### GET `/api/auth/me`
Retrieve authenticated user's profile.

**Request:**
```
Authorization: Bearer <jwt_token>
```

**Response (200):**
```json
{
  "id": "user-uuid-here",
  "email": "user@example.com",
  "name": "Project User",
  "createdAt": "2026-03-15T10:00:00Z"
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### POST `/api/auth/refresh`
Refresh an expired JWT token using httpOnly refresh cookie.

**Request:**
```
(No body; refresh token passed via httpOnly cookie)
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 900
}
```

**Notes:**
- New JWT lifetime: 15 minutes
- Refresh token cookie automatically renewed (7-day lifetime)

**Status Codes:**
- `200` — Token refreshed
- `401` — Refresh token expired or invalid (user must re-authenticate via Google OAuth)

---

### 2.2 Spokes

#### POST `/api/spokes/register`
Register a new spoke with pre-shared key. Returns spoke JWT for use in SignalR connections.

**Request:**
```json
{
  "psk": "pre-shared-key-provided-out-of-band",
  "name": "Work Laptop",
  "capabilities": ["jira", "git", "docker"],
  "os": "linux",
  "architecture": "x64",
  "config": {
    "approvalMode": "plan_review",
    "maxConcurrentJobs": 2,
    "heartbeatIntervalSeconds": 30
  },
  "profile": {
    "displayName": "Development Workstation",
    "machineDescription": "Primary dev machine for full-stack work",
    "repos": [
      {
        "name": "api-service",
        "remoteUrl": "git@github.com:org/api-service.git"
      },
      {
        "name": "web-frontend",
        "remoteUrl": "git@github.com:org/web-frontend.git"
      }
    ],
    "jiraConfig": {
      "instanceUrl": "https://company.atlassian.net",
      "projectKeys": ["PROJ", "INFRA"]
    },
    "integrations": ["github", "jira", "slack"],
    "description": "Workstation for features, testing, and infrastructure work"
  },
  "metadata": {
    "version": "1.0.0",
    "agentType": "claude-code"
  }
}
```

**Response (201):**
```json
{
  "spokeId": "spoke-uuid-1",
  "spokeJwt": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400,
  "profile": {
    "displayName": "Development Workstation",
    "machineDescription": "Primary dev machine for full-stack work",
    "repos": [
      {
        "name": "api-service",
        "remoteUrl": "git@github.com:org/api-service.git"
      },
      {
        "name": "web-frontend",
        "remoteUrl": "git@github.com:org/web-frontend.git"
      }
    ],
    "jiraConfig": {
      "instanceUrl": "https://company.atlassian.net",
      "projectKeys": ["PROJ", "INFRA"]
    },
    "integrations": ["github", "jira", "slack"],
    "description": "Workstation for features, testing, and infrastructure work"
  }
}
```

**Notes:**
- JWT lifetime: 24 hours (86400 seconds)
- PSK only required once per 24-hour cycle; use JWT refresh for subsequent authentications
- Profile object is stored and accessible via GET `/api/spokes/{spokeId}`

**Status Codes:**
- `201` — Spoke registered successfully
- `400` — Invalid request or missing required fields
- `401` — Invalid or expired PSK
- `409` — Spoke with same name already registered

---

#### GET `/api/spokes`
List all registered spokes.

**Query Parameters:**
- `status` (optional) — Filter by status: `online`, `offline`, `busy`
- `limit` (optional, default 50) — Max results
- `offset` (optional, default 0) — Pagination offset

**Response (200):**
```json
{
  "spokes": [
    {
      "id": "spoke-uuid-1",
      "name": "Work Laptop",
      "status": "online",
      "lastSeen": "2026-04-04T14:25:00Z",
      "activeJobCount": 1,
      "capabilities": ["jira", "git", "docker"],
      "config": {
        "approvalMode": "plan_review",
        "maxConcurrentJobs": 2
      }
    },
    {
      "id": "spoke-uuid-2",
      "name": "Personal Dev Server",
      "status": "offline",
      "lastSeen": "2026-04-04T06:15:00Z",
      "activeJobCount": 0,
      "capabilities": ["jira", "git", "docker"],
      "config": {
        "approvalMode": "full_autonomy",
        "maxConcurrentJobs": 3
      }
    }
  ],
  "total": 2,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### GET `/api/spokes/{spokeId}`
Get detailed spoke information with profile.

**Path Parameters:**
- `spokeId` (UUID) — Spoke identifier

**Response (200):**
```json
{
  "id": "spoke-uuid-1",
  "name": "Work Laptop",
  "status": "online",
  "lastSeen": "2026-04-04T14:25:00Z",
  "registeredAt": "2026-03-20T09:30:00Z",
  "activeJobCount": 1,
  "totalJobsCompleted": 47,
  "capabilities": ["jira", "git", "docker"],
  "config": {
    "approvalMode": "plan_review",
    "maxConcurrentJobs": 2,
    "heartbeatIntervalSeconds": 30
  },
  "profile": {
    "displayName": "Development Workstation",
    "machineDescription": "Primary dev machine for full-stack work",
    "repos": [
      {
        "name": "api-service",
        "remoteUrl": "git@github.com:org/api-service.git"
      }
    ],
    "jiraConfig": {
      "instanceUrl": "https://company.atlassian.net",
      "projectKeys": ["PROJ", "INFRA"]
    },
    "integrations": ["github", "jira", "slack"],
    "description": "Workstation for features, testing, and infrastructure work"
  },
  "resourceUsage": {
    "memoryUsageMb": 245,
    "cpuUsagePercent": 12.5,
    "diskUsageMb": 1024
  }
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---

#### PUT `/api/spokes/{spokeId}/config`
Update spoke configuration.

**Path Parameters:**
- `spokeId` (UUID)

**Request:**
```json
{
  "name": "Work Laptop (Updated)",
  "approvalMode": "full_autonomy",
  "maxConcurrentJobs": 3
}
```

**Response (200):**
```json
{
  "id": "spoke-uuid-1",
  "name": "Work Laptop (Updated)",
  "config": {
    "approvalMode": "full_autonomy",
    "maxConcurrentJobs": 3,
    "heartbeatIntervalSeconds": 30
  }
}
```

**Status Codes:**
- `200` — Updated
- `400` — Invalid config value
- `401` — Unauthorized
- `404` — Spoke not found

---

#### DELETE `/api/spokes/{spokeId}`
Deregister a spoke. Cancels all running jobs on that spoke.

**Path Parameters:**
- `spokeId` (UUID)

**Response (204):**
```
(No content)
```

**Status Codes:**
- `204` — Deregistered
- `401` — Unauthorized
- `404` — Spoke not found
- `409` — Spoke has running jobs (requires force flag or manual job cancellation first)

---

#### GET `/api/spokes/{spokeId}/projects`
List projects for a specific spoke.

**Path Parameters:**
- `spokeId` (UUID)

**Query Parameters:**
- `status` (optional) — Filter by status: `planning`, `active`, `paused`, `completed`, `failed`
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "projects": [
    {
      "id": "project-uuid-1",
      "spokeId": "spoke-uuid-1",
      "externalKey": "PROJ-4521",
      "name": "Implement notification service",
      "status": "active",
      "createdAt": "2026-04-01T10:00:00Z",
      "updatedAt": "2026-04-04T14:25:00Z",
      "activeJobCount": 1,
      "totalJobCount": 3
    }
  ],
  "total": 1,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---

#### GET `/api/spokes/{spokeId}/jobs`
List jobs for a specific spoke.

**Path Parameters:**
- `spokeId` (UUID)

**Query Parameters:**
- `status` (optional) — Filter: `queued`, `awaitingApproval`, `running`, `completed`, `failed`, `cancelled`
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "jobs": [
    {
      "id": "job-uuid-1",
      "projectId": "project-uuid-1",
      "spokeId": "spoke-uuid-1",
      "type": "implement",
      "status": "running",
      "createdAt": "2026-04-04T10:00:00Z",
      "startedAt": "2026-04-04T10:05:00Z",
      "completedAt": null,
      "progress": {
        "elapsedSeconds": 1200,
        "estimatedTotalSeconds": 2400
      }
    }
  ],
  "total": 1,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---


#### GET `/api/spokes/{spokeId}/pull-requests`
List monitored pull requests and their comment status for a spoke. Supports GitHub and GitLab. Proposed responses from PR comments route through PendingActions for hub review.

**Path Parameters:**
- `spokeId` (UUID) — Spoke identifier

**Query Parameters:**
- `status` (optional) — Filter by PR status: `open`, `closed`, `all` (default: `open`)
- `provider` (optional) — Filter by provider: `github`, `gitlab`, `all` (default: `all`)
- `repoPath` (optional) — Filter by repository path
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "pullRequests": [
    {
      "provider": "github",
      "number": "42",
      "title": "Add transaction support",
      "repositoryPath": "/home/user/repos/api-service",
      "branch": "feature/transactions",
      "baseRef": "develop",
      "createdAt": "2026-04-03T09:00:00Z",
      "unprocessedCommentCount": 2,
      "comments": [
        {
          "id": "comment-123",
          "author": "reviewer@example.com",
          "body": "Add null check here",
          "classification": "actionable",
          "classificationConfidence": 0.95,
          "detectedAt": "2026-04-04T10:30:00Z",
          "resolvedAt": "2026-04-04T11:00:00Z",
          "resolutionType": "auto_fix_job_created",
          "fixJobId": "job-uuid-1"
        },
        {
          "id": "comment-124",
          "author": "reviewer@example.com",
          "body": "Why not use async/await?",
          "classification": "invalid",
          "classificationConfidence": 0.80,
          "detectedAt": "2026-04-04T10:35:00Z",
          "resolvedAt": null,
          "resolutionType": null
        }
      ]
    }
  ],
  "total": 1,
  "lastMonitoredAt": "2026-04-04T11:30:00Z"
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---

#### GET `/api/spokes/{spokeId}/pull-requests/{prId}/processed-comments`
List processed and unprocessed comments for a specific PR, showing what action was taken on each. Supports GitHub and GitLab.

**Path Parameters:**
- `spokeId` (UUID) — Spoke identifier
- `prId` (string) — Pull request number or ID

**Query Parameters:**
- `provider` (optional) — Filter by provider: `github`, `gitlab` (required if spoke has both GitHub and GitLab integrations)
- `status` (optional) — Filter: `processed`, `unprocessed`, `all` (default: `all`)
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "provider": "github",
  "prId": "42",
  "processedComments": [
    {
      "commentId": "comment-123",
      "prId": "42",
      "author": "reviewer@example.com",
      "body": "Add null check here",
      "classification": "actionable",
      "processedAt": "2026-04-04T11:00:00Z",
      "actionTaken": "pending_action_created",
      "actionMetadata": {
        "pendingActionId": "pa-uuid-1",
        "gateType": "pr_review"
      }
    },
    {
      "commentId": "comment-124",
      "prId": "42",
      "author": "reviewer@example.com",
      "body": "Why not use async/await?",
      "classification": "invalid",
      "processedAt": null,
      "actionTaken": null,
      "actionMetadata": null
    }
  ],
  "total": 2,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke or PR not found

---

### 2.3 Projects

#### GET `/api/projects`
List all projects (cross-spoke).

**Query Parameters:**
- `spokeId` (optional, UUID) — Filter by spoke
- `status` (optional) — Filter by status
- `limit` (optional, default 50)
- `offset` (optional, default 0)
- `sort` (optional) — `created_asc`, `created_desc`, `updated_asc`, `updated_desc` (default: `updated_desc`)

**Response (200):**
```json
{
  "projects": [
    {
      "id": "project-uuid-1",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "externalKey": "PROJ-4521",
      "name": "Implement notification service",
      "status": "active",
      "createdAt": "2026-04-01T10:00:00Z",
      "updatedAt": "2026-04-04T14:25:00Z",
      "activeJobCount": 1,
      "totalJobCount": 3,
      "summary": "Working on async notification system with retry logic"
    }
  ],
  "total": 5,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### GET `/api/projects/{projectId}`
Get detailed project information.

**Path Parameters:**
- `projectId` (UUID)

**Response (200):**
```json
{
  "id": "project-uuid-1",
  "spokeId": "spoke-uuid-1",
  "spokeName": "Work Laptop",
  "externalKey": "PROJ-4521",
  "name": "Implement notification service",
  "status": "active",
  "createdAt": "2026-04-01T10:00:00Z",
  "updatedAt": "2026-04-04T14:25:00Z",
  "summary": "Working on async notification system with retry logic",
  "tickets": {
    "primary": {
      "key": "PROJ-4521",
      "title": "Async notification service with retry",
      "description": "...",
      "status": "In Progress"
    },
    "related": [
      {
        "key": "PROJ-4520",
        "title": "Define notification schema",
        "status": "Done"
      }
    ]
  },
  "jobs": [
    {
      "id": "job-uuid-1",
      "type": "implement",
      "status": "running",
      "createdAt": "2026-04-04T10:00:00Z",
      "startedAt": "2026-04-04T10:05:00Z"
    },
    {
      "id": "job-uuid-2",
      "type": "test",
      "status": "completed",
      "createdAt": "2026-04-03T15:00:00Z"
    }
  ],
  "plan": {
    "generated": true,
    "generatedAt": "2026-04-01T11:00:00Z",
    "content": "## Implementation Plan\n\n1. Design notification schema\n2. Implement async queue\n3. Add retry logic\n4. Write tests"
  }
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Project not found

---

#### POST `/api/projects`
Create a new project on a spoke.

**Request:**
```json
{
  "spokeId": "spoke-uuid-1",
  "externalKey": "PROJ-4521",
  "name": "Implement notification service",
  "summary": "Async notification system with retry logic",
  "approvalMode": "plan_review"
}
```

**Response (201):**
```json
{
  "id": "project-uuid-1",
  "spokeId": "spoke-uuid-1",
  "externalKey": "PROJ-4521",
  "name": "Implement notification service",
  "status": "planning",
  "createdAt": "2026-04-04T14:30:00Z"
}
```

**Status Codes:**
- `201` — Created
- `400` — Invalid request
- `401` — Unauthorized
- `404` — Spoke not found
- `409` — Project with this external key already exists on spoke

---

#### PUT `/api/projects/{projectId}/status`
Update project status.

**Path Parameters:**
- `projectId` (UUID)

**Request:**
```json
{
  "status": "paused",
  "reason": "Waiting for API design review"
}
```

**Response (200):**
```json
{
  "id": "project-uuid-1",
  "status": "paused",
  "updatedAt": "2026-04-04T14:35:00Z"
}
```

**Status Codes:**
- `200` — Updated
- `400` — Invalid status value
- `401` — Unauthorized
- `404` — Project not found

---

### 2.4 Jobs

#### GET `/api/jobs`
List jobs with filters.

**Query Parameters:**
- `spokeId` (optional, UUID) — Filter by spoke
- `projectId` (optional, UUID) — Filter by project
- `status` (optional) — Filter: `queued`, `awaitingApproval`, `running`, `completed`, `failed`, `cancelled`
- `type` (optional) — Filter: `implement`, `test`, `refactor`, `investigate`, `custom`
- `limit` (optional, default 50)
- `offset` (optional, default 0)
- `sort` (optional) — `created_asc`, `created_desc`, `started_asc`, `started_desc`, `completed_asc`, `completed_desc`

**Response (200):**
```json
{
  "jobs": [
    {
      "id": "job-uuid-1",
      "projectId": "project-uuid-1",
      "spokeId": "spoke-uuid-1",
      "type": "implement",
      "status": "running",
      "createdAt": "2026-04-04T10:00:00Z",
      "startedAt": "2026-04-04T10:05:00Z",
      "completedAt": null,
      "progress": {
        "elapsedSeconds": 1200,
        "estimatedTotalSeconds": 2400
      }
    }
  ],
  "total": 47,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### GET `/api/jobs/{jobId}`
Get detailed job information.

**Path Parameters:**
- `jobId` (UUID)

**Response (200):**
```json
{
  "id": "job-uuid-1",
  "projectId": "project-uuid-1",
  "spokeId": "spoke-uuid-1",
  "type": "implement",
  "status": "running",
  "createdAt": "2026-04-04T10:00:00Z",
  "startedAt": "2026-04-04T10:05:00Z",
  "completedAt": null,
  "prompt": "## Task\n\nImplement async notification service...",
  "summary": null,
  "outputChunkCount": 142,
  "outputTotalBytes": 45328,
  "progress": {
    "elapsedSeconds": 1200,
    "estimatedTotalSeconds": 2400
  },
  "metadata": {
    "containerId": "worker-abc123",
    "branch": "feature/notification-service"
  }
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Job not found

---

#### POST `/api/jobs`
Create and assign a new job.

**Headers:**
- `Idempotency-Key` (optional, UUID) — Idempotency token. If provided and a job with same key exists, returns existing job (200) instead of creating duplicate.

**Request:**
```json
{
  "projectId": "project-uuid-1",
  "type": "implement",
  "requiresApproval": true,
  "context": {
    "ticketKey": "PROJ-4521",
    "description": "Implement notification service with retries"
  }
}
```

**Response (201):**
```json
{
  "id": "job-uuid-1",
  "projectId": "project-uuid-1",
  "spokeId": "spoke-uuid-1",
  "type": "implement",
  "status": "awaiting_approval",
  "createdAt": "2026-04-04T14:40:00Z"
}
```

**Response (200 — Idempotent):**
If `Idempotency-Key` matches an existing job, returns the existing job with status 200.

**Status Codes:**
- `201` — Created
- `200` — Job already exists for this Idempotency-Key (idempotent retry)
- `400` — Invalid request
- `401` — Unauthorized
- `404` — Project not found
- `409` — Spoke offline or at max concurrency

---

#### PUT `/api/jobs/{jobId}/approve`
Approve a job awaiting approval.

**Path Parameters:**
- `jobId` (UUID)

**Request:**
```json
{
  "approved": true,
  "modifications": {
    "notes": "Looks good, proceed with implementation"
  }
}
```

**Response (200):**
```json
{
  "id": "job-uuid-1",
  "status": "queued",
  "updatedAt": "2026-04-04T14:42:00Z"
}
```

**Status Codes:**
- `200` — Approved/rejected
- `400` — Job not in awaiting_approval status
- `401` — Unauthorized
- `404` — Job not found

---

#### PUT `/api/jobs/{jobId}/cancel`
Cancel a job (queued, awaiting_approval, or running).

**Path Parameters:**
- `jobId` (UUID)

**Request:**
```json
{
  "reason": "Out of scope, deprioritized"
}
```

**Response (200):**
```json
{
  "id": "job-uuid-1",
  "status": "cancelled",
  "cancelledAt": "2026-04-04T14:43:00Z"
}
```

**Status Codes:**
- `200` — Cancelled
- `400` — Job in terminal state (completed, failed, or already cancelled)
- `401` — Unauthorized
- `404` — Job not found

---

#### GET `/api/jobs/{jobId}/output`
Get job terminal output (paginated).

**Path Parameters:**
- `jobId` (UUID)

**Query Parameters:**
- `limit` (optional, default 100) — Max lines to return (max 1000)
- `offset` (optional, default 0) — Start from chunk N
- `follow` (optional, default false) — If true and job running, establish Server-Sent Events stream for live output

**Response (200) — Static:**
```json
{
  "jobId": "job-uuid-1",
  "chunks": [
    {
      "sequence": 0,
      "content": "Claude Code worker starting...\n",
      "timestamp": "2026-04-04T10:05:01Z"
    },
    {
      "sequence": 1,
      "content": "Loading project context...\n",
      "timestamp": "2026-04-04T10:05:02Z"
    }
  ],
  "totalChunks": 142,
  "limit": 100,
  "offset": 0,
  "isComplete": false
}
```

**Response (200) — Server-Sent Events (when `follow=true`):**
```
event: output
data: {"sequence": 142, "content": "Building...\n", "timestamp": "2026-04-04T10:25:30Z"}

event: status
data: {"status": "completed", "completedAt": "2026-04-04T10:35:00Z"}

event: end
data: {}
```

**Status Codes:**
- `200` — OK (static) or stream started (SSE)
- `401` — Unauthorized
- `404` — Job not found

---

### 2.5 Conversations

#### GET `/api/conversations`
List all conversations (hub-level and spoke-specific).

**Query Parameters:**
- `spokeId` (optional, UUID) — Filter by spoke (null = hub-level conversations)
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "conversations": [
    {
      "id": "conv-uuid-1",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "title": "Notification service implementation",
      "createdAt": "2026-04-04T10:00:00Z",
      "updatedAt": "2026-04-04T14:30:00Z",
      "ccSessionId": "cc-session-abc123",
      "messageCount": 47
    },
    {
      "id": "conv-uuid-2",
      "spokeId": null,
      "spokeName": null,
      "title": "Cross-system query: status across all spokes",
      "createdAt": "2026-04-03T09:00:00Z",
      "updatedAt": "2026-04-04T14:00:00Z",
      "ccSessionId": "cc-session-def456",
      "messageCount": 12
    }
  ],
  "total": 2,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### GET `/api/conversations/{conversationId}`
Get conversation with full message history.

**Path Parameters:**
- `conversationId` (UUID)

**Query Parameters:**
- `limit` (optional, default 50) — Messages per page
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "id": "conv-uuid-1",
  "spokeId": "spoke-uuid-1",
  "spokeName": "Work Laptop",
  "title": "Notification service implementation",
  "createdAt": "2026-04-04T10:00:00Z",
  "updatedAt": "2026-04-04T14:30:00Z",
  "ccSessionId": "cc-session-abc123",
  "messages": [
    {
      "id": "msg-uuid-1",
      "conversationId": "conv-uuid-1",
      "role": "user",
      "content": "Start implementing the notification service for PROJ-4521",
      "timestamp": "2026-04-04T10:05:00Z"
    },
    {
      "id": "msg-uuid-2",
      "conversationId": "conv-uuid-1",
      "role": "assistant",
      "content": "I'll begin by analyzing the requirements and creating an implementation plan...",
      "timestamp": "2026-04-04T10:05:30Z"
    }
  ],
  "messageCount": 47
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Conversation not found

---

#### POST `/api/conversations`
Start a new conversation on a spoke or hub-level.

**Request:**
```json
{
  "spokeId": "spoke-uuid-1",
  "title": "Notification service implementation"
}
```

**Response (201):**
```json
{
  "id": "conv-uuid-1",
  "spokeId": "spoke-uuid-1",
  "spokeName": "Work Laptop",
  "title": "Notification service implementation",
  "createdAt": "2026-04-04T14:40:00Z",
  "ccSessionId": "cc-session-abc123",
  "messageCount": 0
}
```

**Status Codes:**
- `201` — Created
- `400` — Invalid request
- `401` — Unauthorized
- `404` — Spoke not found (if spokeId provided)

---

#### DELETE `/api/conversations/{conversationId}`
Archive a conversation (soft delete).

**Path Parameters:**
- `conversationId` (UUID)

**Response (204):**
```
(No content)
```

**Status Codes:**
- `204` — Archived
- `401` — Unauthorized
- `404` — Conversation not found

---

#### POST `/api/conversations/{conversationId}/messages`
Send a message in a conversation (routed to appropriate CC instance).

**Path Parameters:**
- `conversationId` (UUID)

**Request:**
```json
{
  "content": "What's the status of the notification service implementation?"
}
```

**Response (201):**
```json
{
  "id": "msg-uuid-3",
  "conversationId": "conv-uuid-1",
  "role": "user",
  "content": "What's the status of the notification service implementation?",
  "timestamp": "2026-04-04T14:45:00Z"
}
```

**Status Codes:**
- `201` — Message sent (will trigger async CC invocation)
- `400` — Invalid request
- `401` — Unauthorized
- `404` — Conversation not found
- `503` — Spoke offline or CC unavailable

**Note:** Response acknowledges message acceptance. Spoke/hub CC instance processes asynchronously. Response will be streamed back via SignalR `ConversationMessageReceived` event.

---

### 2.6 Messages

#### GET `/api/spokes/{spokeId}/messages`
Get conversation history with a spoke (deprecated; use `/api/conversations` instead).

**Path Parameters:**
- `spokeId` (UUID)

**Query Parameters:**
- `limit` (optional, default 50)
- `offset` (optional, default 0)
- `jobId` (optional, UUID) — Filter to messages related to a specific job

**Response (200):**
```json
{
  "messages": [
    {
      "id": "msg-uuid-1",
      "spokeId": "spoke-uuid-1",
      "direction": "user_to_spoke",
      "content": "What's the status of PROJ-4521?",
      "timestamp": "2026-04-04T14:00:00Z",
      "jobId": null
    },
    {
      "id": "msg-uuid-2",
      "spokeId": "spoke-uuid-1",
      "direction": "spoke_to_user",
      "content": "PROJ-4521 is actively in progress. We've implemented the core async queue and are working on retry logic. Currently in job-uuid-1, ~50% complete.",
      "timestamp": "2026-04-04T14:00:05Z",
      "jobId": "job-uuid-1"
    }
  ],
  "total": 47,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---

#### POST `/api/spokes/{spokeId}/messages` (DEPRECATED)
**DEPRECATED.** Use `/api/conversations/{conversationId}/messages` instead.

Send a message to a spoke.

**Path Parameters:**
- `spokeId` (UUID)

**Request:**
```json
{
  "content": "What's blocking PROJ-4587?",
  "jobId": null
}
```

**Response (201):**
```json
{
  "id": "msg-uuid-3",
  "spokeId": "spoke-uuid-1",
  "direction": "user_to_spoke",
  "content": "What's blocking PROJ-4587?",
  "timestamp": "2026-04-04T14:01:00Z"
}
```

**Migration Path:**
Create a conversation for the spoke first via `/api/conversations`, then use `/api/conversations/{conversationId}/messages` for all future messages.

**Status Codes:**
- `201` — Message sent
- `400` — Invalid request
- `401` — Unauthorized
- `404` — Spoke not found
- `503` — Spoke offline (message queued for delivery on reconnect)

---

### 2.7 Pending Actions (Awaiting Input Queue)

#### GET `/api/pending-actions`
Get all items awaiting human-in-the-loop (HITL) attention across all spokes. Approval gates fire between job phases (plan → implement → PR), not on individual CC tool calls.

**Query Parameters:**
- `gateType` (optional) — Filter by gate type: `plan_review`, `pre_execution`, `post_execution`, `spoke_question`, `pr_review`
- `spokeId` (optional, UUID) — Filter by spoke
- `projectId` (optional, UUID) — Filter by project
- `sort` (optional) — `age_asc`, `age_desc` (default: `age_desc` = oldest first)
- `limit` (optional, default 50)
- `offset` (optional, default 0)

**Response (200):**
```json
{
  "pendingActions": [
    {
      "id": "pa-uuid-1",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "projectId": "project-uuid-1",
      "externalKey": "PROJ-4521",
      "gateType": "plan_review",
      "summary": "Implementation plan for async notification service ready for review",
      "description": "Scope: async queue, retry logic, monitoring. Approach: Redis-backed queue with exponential backoff.",
      "createdAt": "2026-04-04T10:30:00Z",
      "age": "3h 45m",
      "metadata": {
        "jobId": "job-uuid-1",
        "planContent": "## Implementation Plan\n\n1. Design schema\n2. Implement queue\n3. Add retry logic",
        "approvalRequired": true
      }
    },
    {
      "id": "pa-uuid-2",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "projectId": "project-uuid-2",
      "externalKey": "PROJ-4585",
      "gateType": "post_execution",
      "summary": "Job completed: feature branch ready for review",
      "description": "Branch: feature/search-optimization. Changes: 3 files, +145 lines. Tests: 12 new, all passing.",
      "createdAt": "2026-04-04T13:15:00Z",
      "age": "1h 0m",
      "metadata": {
        "jobId": "job-uuid-3",
        "branchName": "feature/search-optimization",
        "prUrl": "https://github.com/repo/pulls/89",
        "filesChanged": 3,
        "testsAdded": 12,
        "summary": "Implemented indexed search with caching layer"
      }
    },
    {
      "id": "pa-uuid-3",
      "spokeId": "spoke-uuid-2",
      "spokeName": "Personal Dev Server",
      "projectId": "project-uuid-3",
      "externalKey": "INFRA-122",
      "gateType": "spoke_question",
      "summary": "Blocker: Database migration strategy unclear",
      "description": "Can't proceed with schema changes. Need guidance on handling existing data migration without downtime.",
      "createdAt": "2026-04-04T13:45:00Z",
      "age": "30m",
      "metadata": {
        "jobId": "job-uuid-5",
        "questionType": "blocker",
        "context": "Database migration for INFRA-122 requires zero-downtime strategy"
      }
    }
  ],
  "total": 3,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

#### POST `/api/pending-actions/{id}/resolve`
Resolve a pending action (approve, reject, or respond).

**Path Parameters:**
- `id` (UUID) — Pending action ID

**Request:**
```json
{
  "action": "approve",
  "notes": "Looks good, proceed with implementation",
  "modifications": null
}
```

**Request Notes:**
- `action` (required) — One of: `approve`, `reject`, `respond`
- `notes` (optional) — User comment for the spoke or for history
- `modifications` (optional) — For `approve` with plan review, optional modifications to the plan before approval

**Response (200):**
```json
{
  "id": "pa-uuid-1",
  "status": "resolved",
  "action": "approve",
  "resolvedAt": "2026-04-04T14:45:00Z",
  "metadata": {
    "jobId": "job-uuid-1",
    "notes": "Looks good, proceed with implementation"
  }
}
```

**Status Codes:**
- `200` — Resolved
- `400` — Invalid action value or invalid state transition
- `401` — Unauthorized
- `404` — Pending action not found
- `409` — Action already resolved

---

### 2.7a Pending Commands (Spoke Command Queue)

#### GET `/api/spokes/{spokeId}/pending-commands`
Get undelivered commands for a spoke. Commands have 24-hour time-to-live (TTL).

**Path Parameters:**
- `spokeId` (UUID) — Spoke identifier

**Query Parameters:**
- `limit` (optional, default 50) — Max results
- `offset` (optional, default 0) — Pagination offset

**Response (200):**
```json
{
  "commands": [
    {
      "id": "cmd-uuid-1",
      "type": "start_job",
      "payload": {
        "jobId": "job-uuid-1",
        "prompt": "Implement notification service...",
        "approvalRequired": false
      },
      "createdAt": "2026-04-04T14:45:00Z",
      "deliveryAttempts": 0,
      "expiresAt": "2026-04-05T14:45:00Z"
    },
    {
      "id": "cmd-uuid-2",
      "type": "cancel_job",
      "payload": {
        "jobId": "job-uuid-2",
        "reason": "Deprioritized"
      },
      "createdAt": "2026-04-04T14:46:00Z",
      "deliveryAttempts": 1,
      "expiresAt": "2026-04-05T14:46:00Z"
    }
  ],
  "total": 2,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized
- `404` — Spoke not found

---

#### POST `/api/spokes/{spokeId}/pending-commands/{commandId}/acknowledge`
Confirm receipt of a command. Marks command as delivered; hub removes from queue.

**Path Parameters:**
- `spokeId` (UUID) — Spoke identifier
- `commandId` (UUID) — Command identifier

**Request:**
```json
{
  "acknowledged": true
}
```

**Response (200):**
```json
{
  "id": "cmd-uuid-1",
  "acknowledged": true,
  "acknowledgedAt": "2026-04-04T14:45:15Z"
}
```

**Status Codes:**
- `200` — Acknowledged
- `400` — Command already acknowledged
- `401` — Unauthorized
- `404` — Spoke or command not found
- `410` — Command expired (TTL exceeded)

---

### 2.8 Timeline

#### GET `/api/timeline`
Cross-spoke activity feed (paginated, filterable).

**Query Parameters:**
- `spokeId` (optional, UUID) — Filter by spoke
- `eventType` (optional) — Filter: `project_created`, `project_updated`, `job_created`, `job_status_changed`, `message_sent`, `spoke_connected`, `spoke_disconnected`
- `limit` (optional, default 50)
- `offset` (optional, default 0)
- `since` (optional, RFC3339 timestamp) — Events after this time
- `until` (optional, RFC3339 timestamp) — Events before this time

**Response (200):**
```json
{
  "events": [
    {
      "id": "event-uuid-1",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "type": "job_created",
      "timestamp": "2026-04-04T14:40:00Z",
      "summary": "Job created: implement (PROJ-4521)",
      "metadata": {
        "jobId": "job-uuid-1",
        "projectId": "project-uuid-1",
        "jobType": "implement"
      }
    },
    {
      "id": "event-uuid-2",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "type": "job_status_changed",
      "timestamp": "2026-04-04T14:42:00Z",
      "summary": "Job approved and queued",
      "metadata": {
        "jobId": "job-uuid-1",
        "newStatus": "queued",
        "previousStatus": "awaiting_approval"
      }
    },
    {
      "id": "event-uuid-3",
      "spokeId": "spoke-uuid-1",
      "spokeName": "Work Laptop",
      "type": "job_status_changed",
      "timestamp": "2026-04-04T14:45:00Z",
      "summary": "Job started",
      "metadata": {
        "jobId": "job-uuid-1",
        "newStatus": "running",
        "previousStatus": "queued"
      }
    }
  ],
  "total": 237,
  "limit": 50,
  "offset": 0
}
```

**Status Codes:**
- `200` — OK
- `401` — Unauthorized

---

## 3. Data Transfer Objects (DTOs)

### 3.1 Conversation DTOs

**C# (Domain Models):**
```csharp
public record Conversation(
    Guid Id,
    Guid? SpokeId,
    string? SpokeName,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CcSessionId,
    int MessageCount
);

public record ConversationMessage(
    Guid Id,
    Guid ConversationId,
    string Role,  // "user" or "assistant"
    string Content,
    DateTime Timestamp
);

public record CreateConversationRequest(
    Guid? SpokeId,
    string Title
);

public record SendMessageRequest(
    string Content
);
```

**TypeScript (Generated from OpenAPI):**
```typescript
interface Conversation {
  id: string;
  spokeId?: string | null;
  spokeName?: string | null;
  title: string;
  createdAt: string;  // ISO 8601
  updatedAt: string;
  ccSessionId: string;
  messageCount: number;
}

interface ConversationMessage {
  id: string;
  conversationId: string;
  role: "user" | "assistant";
  content: string;
  timestamp: string;  // ISO 8601
}

interface ConversationResponse {
  id: string;
  spokeId?: string | null;
  spokeName?: string | null;
  title: string;
  createdAt: string;
  updatedAt: string;
  ccSessionId: string;
  messages: ConversationMessage[];
  messageCount: number;
}
```

---

### 3.2 SignalR Events in REST

#### Hub → Client: `ConversationMessageReceived`

Broadcasted when a response is received from a spoke or hub CC instance.

**Payload:**
```json
{
  "conversationId": "conv-uuid-1",
  "messageId": "msg-uuid-2",
  "role": "assistant",
  "content": "Here's the implementation plan...",
  "timestamp": "2026-04-04T10:05:30Z",
  "streaming": false
}
```

**Use:** Update conversation UI with new message in real-time.

#### Spoke → Hub → Client: `ConversationStreamChunk` (Future)

For very long responses, chunks can be streamed (Server-Sent Events or WebSocket). Define later in Phase 4.

---

## 4. SignalR Hub Contract

**Note on MCPs & Hub-Local Tools:** Spoke MCPs (Jira, Git, file system) are LOCAL to the spoke only and never exposed to the hub. The hub CC meta-agent queries spokes using hub-local tools (not MCP connections), which proxy requests over the existing SignalR WebSocket. The hub never initiates direct connections to spoke MCPs. See [Security Model](./security-model.md) for architecture details.

### 4.1 Hub Configuration

**Hub URL:** `wss://<hub-host>/hubs/nexus`

**Authentication:** Bearer token in query string or header (SignalR best practice varies by client).

```csharp
// C# Client Example
var connection = new HubConnectionBuilder()
    .WithUrl("wss://hub.example.com/hubs/nexus", options =>
    {
        options.Headers["Authorization"] = $"Bearer {spokeToken}";
    })
    .WithAutomaticReconnect()
    .Build();
```

**Reconnection Policy:** Automatic exponential backoff — 0s, 1s, 3s, 5s, 10s, 30s, then every 30s indefinitely.

---

### 4.2 Spoke → Hub (Server Methods)

#### RegisterSpoke
Initial registration by spoke daemon at startup.

**Signature:**
```csharp
public async Task RegisterSpoke(SpokeRegistration registration)
{
    // Spoke provides its identity, capabilities, and config.
    // Hub acknowledges and stores in database.
}
```

**SpokeRegistration (C#):**
```csharp
public record SpokeRegistration(
    string Name,                           // e.g., "Work Laptop"
    string[] Capabilities,                 // ["jira", "git", "docker"]
    string OS,                             // "windows", "macos", "linux" — identifies host platform
    string Architecture,                   // "x64", "arm64" — spoke architecture
    SpokeConfigDto Config,
    SpokeProfileDto Profile,               // Profile object with repos, Jira config, integrations
    Dictionary<string, string> Metadata    // Custom key-value pairs
);

public record SpokeConfigDto(
    ApprovalMode ApprovalMode,             // "plan_review", "full_autonomy"
    int MaxConcurrentJobs,
    int HeartbeatIntervalSeconds
);

public record SpokeProfileDto(
    string DisplayName,                    // e.g., "Development Workstation"
    string MachineDescription,             // Human-readable machine description
    RepositoryDto[] Repos,                 // Array of {name, remote_url}
    JiraConfigDto JiraConfig,              // {instance_url, project_keys[]}
    string[] Integrations,                 // ["github", "jira", "slack"]
    string Description                     // Free-text description
);

public record RepositoryDto(
    string Name,
    string RemoteUrl
);

public record JiraConfigDto(
    string InstanceUrl,
    string[] ProjectKeys
);

public enum ApprovalMode
{
    PlanReview = 0,      // Human reviews plan before job starts
    FullAutonomy = 1,    // Job runs immediately; only notify on errors
    CustomPerJob = 2     // Per-job configuration
}
```

**SpokeRegistration (TypeScript):**
```typescript
interface SpokeRegistration {
  name: string;
  capabilities: string[];
  os: "windows" | "macos" | "linux";     // Host platform (Windows, macOS, Linux)
  architecture: "x64" | "arm64";         // Processor architecture
  config: SpokeConfigDto;
  profile: SpokeProfileDto;
  metadata: Record<string, string>;
}

interface SpokeProfileDto {
  displayName: string;
  machineDescription: string;
  repos: RepositoryDto[];
  jiraConfig: JiraConfigDto;
  integrations: string[];
  description: string;
}

interface RepositoryDto {
  name: string;
  remoteUrl: string;
}

interface JiraConfigDto {
  instanceUrl: string;
  projectKeys: string[];
}

interface SpokeConfigDto {
  approvalMode: "plan_review" | "full_autonomy" | "custom_per_job";
  maxConcurrentJobs: number;
  heartbeatIntervalSeconds: number;
}
```

**Hub Response (via callback):**
```csharp
public async Task SpokeRegistered(SpokeInfo spokeInfo)
{
    // spokeInfo contains the spoke's ID and hub-assigned configuration
}
```

---

#### Heartbeat
Periodic health check from spoke (every 30 seconds recommended).

**Signature:**
```csharp
public async Task Heartbeat(SpokeHeartbeat heartbeat)
{
    // Spoke reports status, resource usage, and active job count.
    // Hub updates last_seen timestamp and broadcasts status to UI.
}
```

**SpokeHeartbeat (C#):**
```csharp
public record SpokeHeartbeat(
    Guid SpokeId,
    SpokeStatus Status,                    // online, offline, busy
    int ActiveJobCount,
    ResourceUsage ResourceUsage,
    DateTime Timestamp
);

public enum SpokeStatus
{
    Online = 0,
    Offline = 1,
    Busy = 2
}

public record ResourceUsage(
    long MemoryUsageMb,
    double CpuUsagePercent,
    long DiskUsageMb
);
```

**SpokeHeartbeat (TypeScript):**
```typescript
interface SpokeHeartbeat {
  spokeId: string;
  status: "online" | "offline" | "busy";
  activeJobCount: number;
  resourceUsage: ResourceUsage;
  timestamp: string; // ISO 8601
}

interface ResourceUsage {
  memoryUsageMb: number;
  cpuUsagePercent: number;
  diskUsageMb: number;
}
```

**Hub Response:**
```csharp
public async Task HeartbeatAcknowledged(Guid spokeId, DateTime timestamp)
{
    // Simple ack
}
```

---

#### ReportSpokeStatus
Explicit status change (online → offline, busy, etc.).

**Signature:**
```csharp
public async Task ReportSpokeStatus(SpokeStatusUpdate update)
{
    // Status change due to event (e.g., spoke going offline, becoming busy).
}
```

**SpokeStatusUpdate (C#):**
```csharp
public record SpokeStatusUpdate(
    Guid SpokeId,
    SpokeStatus NewStatus,
    SpokeStatus? PreviousStatus,
    string? Reason,                        // e.g., "Shutting down", "System overload"
    DateTime Timestamp
);
```

**SpokeStatusUpdate (TypeScript):**
```typescript
interface SpokeStatusUpdate {
  spokeId: string;
  newStatus: "online" | "offline" | "busy";
  previousStatus?: "online" | "offline" | "busy";
  reason?: string;
  timestamp: string;
}
```

---

#### QuerySpokeStatus
Query cached spoke state without CC involvement. Returns immediately from hub's in-memory cache.

**Signature:**
```csharp
public async Task<SpokeStatusSnapshot> QuerySpokeStatus(Guid spokeId)
{
    // Returns cached spoke state immediately
    // No CC query; purely informational
}
```

**SpokeStatusSnapshot (C#):**
```csharp
public record SpokeStatusSnapshot(
    Guid SpokeId,
    string Name,
    SpokeStatus Status,
    int ActiveJobCount,
    List<ProjectSummary> Projects,
    ResourceUsage ResourceUsage,
    DateTime LastSeen
);

public record ProjectSummary(
    Guid ProjectId,
    string Name,
    int ActiveJobCount
);
```

**SpokeStatusSnapshot (TypeScript):**
```typescript
interface SpokeStatusSnapshot {
  spokeId: string;
  name: string;
  status: "online" | "offline" | "busy";
  activeJobCount: number;
  projects: ProjectSummary[];
  resourceUsage: ResourceUsage;
  lastSeen: string; // ISO 8601
}

interface ProjectSummary {
  projectId: string;
  name: string;
  activeJobCount: number;
}
```

---

#### QuerySpoke
Full-fledged query of a spoke with CC involvement. Asynchronous; returns correlation_id immediately. Response arrives via `SpokeQueryResponse` event.

**Signature:**
```csharp
public async Task<QueryResponse> QuerySpoke(Guid spokeId, string question)
{
    // Hub routes question to spoke's CC agent
    // Returns correlation_id immediately
    // Spoke processes and sends SpokeQueryResponse event with results
}
```

**QueryResponse (C#):**
```csharp
public record QueryResponse(
    string CorrelationId,
    Guid SpokeId,
    string Question,
    DateTime InitiatedAt
);
```

**QueryResponse (TypeScript):**
```typescript
interface QueryResponse {
  correlationId: string;
  spokeId: string;
  question: string;
  initiatedAt: string; // ISO 8601
}
```

**Notes:**
- Query is async; listen for `SpokeQueryResponse` event with matching correlationId
- Useful for real-time spoke diagnostics, project state queries, or capability checks
- Different from `QuerySpokeStatus`: this one invokes CC reasoning, has latency

---

#### ReportProjectCreated
Spoke notifies hub that a new project has been created locally.

**Signature:**
```csharp
public async Task ReportProjectCreated(ProjectCreatedEvent evt)
{
}
```

**ProjectCreatedEvent (C#):**
```csharp
public record ProjectCreatedEvent(
    Guid ProjectId,
    Guid SpokeId,
    string ExternalKey,                    // e.g., "PROJ-4521"
    string Name,
    string? Summary,
    ProjectStatus Status,                  // planning, active, paused, completed, failed
    DateTime CreatedAt
);

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4
}
```

**ProjectCreatedEvent (TypeScript):**
```typescript
interface ProjectCreatedEvent {
  projectId: string;
  spokeId: string;
  externalKey: string;
  name: string;
  summary?: string;
  status: "planning" | "active" | "paused" | "completed" | "failed";
  createdAt: string;
}
```

---

#### ReportProjectUpdated
Spoke notifies hub of project status/metadata changes.

**Signature:**
```csharp
public async Task ReportProjectUpdated(ProjectUpdatedEvent evt)
{
}
```

**ProjectUpdatedEvent (C#):**
```csharp
public record ProjectUpdatedEvent(
    Guid ProjectId,
    Guid SpokeId,
    ProjectStatus? NewStatus,
    string? NewName,
    string? NewSummary,
    Dictionary<string, object>? Metadata,
    DateTime UpdatedAt
);
```

**ProjectUpdatedEvent (TypeScript):**
```typescript
interface ProjectUpdatedEvent {
  projectId: string;
  spokeId: string;
  newStatus?: "planning" | "active" | "paused" | "completed" | "failed";
  newName?: string;
  newSummary?: string;
  metadata?: Record<string, any>;
  updatedAt: string;
}
```

---

#### ReportJobCreated
Spoke notifies hub that a new job has been created.

**Signature:**
```csharp
public async Task ReportJobCreated(JobCreatedEvent evt)
{
}
```

**JobCreatedEvent (C#):**
```csharp
public record JobCreatedEvent(
    Guid JobId,
    Guid ProjectId,
    Guid SpokeId,
    JobType Type,
    JobStatus Status,                      // queued, awaiting_approval, running, completed, failed, cancelled
    string? Prompt,
    DateTime CreatedAt
);

public enum JobType
{
    Implement = 0,
    Test = 1,
    Refactor = 2,
    Investigate = 3,
    Custom = 4
}

public enum JobStatus
{
    Queued = 0,
    AwaitingApproval = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
```

**JobCreatedEvent (TypeScript):**
```typescript
interface JobCreatedEvent {
  jobId: string;
  projectId: string;
  spokeId: string;
  type: "implement" | "test" | "refactor" | "investigate" | "custom";
  status: "queued" | "awaitingApproval" | "running" | "completed" | "failed" | "cancelled";
  prompt?: string;
  createdAt: string;
}
```

---

#### ReportJobStatusChanged
Spoke notifies hub of job status changes (queued → running, running → completed, etc.).

**Signature:**
```csharp
public async Task ReportJobStatusChanged(JobStatusChangedEvent evt)
{
}
```

**JobStatusChangedEvent (C#):**
```csharp
public record JobStatusChangedEvent(
    Guid JobId,
    Guid ProjectId,
    Guid SpokeId,
    JobStatus NewStatus,
    JobStatus PreviousStatus,
    string? Summary,                       // Agent-generated outcome summary (on completion)
    Dictionary<string, object>? Metadata,
    DateTime Timestamp
);
```

**JobStatusChangedEvent (TypeScript):**
```typescript
interface JobStatusChangedEvent {
  jobId: string;
  projectId: string;
  spokeId: string;
  newStatus: "queued" | "awaitingApproval" | "running" | "completed" | "failed" | "cancelled";
  previousStatus: "queued" | "awaitingApproval" | "running" | "completed" | "failed" | "cancelled";
  summary?: string;
  metadata?: Record<string, any>;
  timestamp: string;
}
```

---

#### StreamJobOutput
Spoke streams terminal output from a worker container in real-time.

**Signature:**
```csharp
public async Task StreamJobOutput(JobOutputChunk chunk)
{
    // Called for each output chunk during job execution.
}
```

**JobOutputChunk (C#):**
```csharp
public record JobOutputChunk(
    Guid JobId,
    Guid SpokeId,
    int Sequence,                          // Monotonic counter for ordering
    string Content,                        // Raw stdout/stderr
    OutputStreamType StreamType,           // stdout, stderr
    DateTime Timestamp
);

public enum OutputStreamType
{
    Stdout = 0,
    Stderr = 1
}
```

**JobOutputChunk (TypeScript):**
```typescript
interface JobOutputChunk {
  jobId: string;
  spokeId: string;
  sequence: number;
  content: string;
  streamType: "stdout" | "stderr";
  timestamp: string;
}
```

**Hub Behavior:** Buffers chunks (e.g., up to 50KB or 100 chunks) before persisting to avoid database load. On reception, broadcasts immediately to all connected UI clients viewing that job.

---

#### SendMessageFromSpoke
Spoke sends a conversational response to a message from the user.

**Signature:**
```csharp
public async Task SendMessageFromSpoke(SpokeMessage message)
{
    // Spoke replies to a user message or initiates a message.
}
```

**SpokeMessage (C#):**
```csharp
public record SpokeMessage(
    Guid SpokeId,
    string Content,
    Guid? JobId,                           // Optional FK if message relates to a job
    Dictionary<string, object>? Metadata,
    DateTime Timestamp
);
```

**SpokeMessage (TypeScript):**
```typescript
interface SpokeMessage {
  spokeId: string;
  content: string;
  jobId?: string;
  metadata?: Record<string, any>;
  timestamp: string;
}
```

---


#### PrCommentDetected
Spoke notifies hub when it detects a new review comment on a PR.

**Signature:**
```csharp
public async Task PrCommentDetected(PrCommentEvent evt)
{
    // Spoke reports a new PR comment for hub awareness.
}
```

**PrCommentEvent (C#):**
```csharp
public record PrCommentEvent(
    Guid SpokeId,
    string RepositoryPath,
    string PrNumber,
    string PrTitle,
    string CommentId,
    string CommentAuthor,
    string CommentBody,
    string Classification,                 // "actionable", "invalid", "positive", "ambiguous"
    double ClassificationConfidence,       // 0.0 to 1.0
    DateTime DetectedAt
);
```

**PrCommentEvent (TypeScript):**
```typescript
interface PrCommentEvent {
  spokeId: string;
  repositoryPath: string;
  prNumber: string;
  prTitle: string;
  commentId: string;
  commentAuthor: string;
  commentBody: string;
  classification: "actionable" | "invalid" | "positive" | "ambiguous";
  classificationConfidence: number;
  detectedAt: string;
}
```

---

#### PrFixJobCreated
Spoke notifies hub when it auto-creates a fix job for an actionable PR comment.

**Signature:**
```csharp
public async Task PrFixJobCreated(PrFixJobEvent evt)
{
    // Spoke reports auto-created fix job for PR comment.
}
```

**PrFixJobEvent (C#):**
```csharp
public record PrFixJobEvent(
    Guid SpokeId,
    Guid JobId,
    string RepositoryPath,
    string PrNumber,
    string CommentId,
    string SuggestedAction,                // Description of fix from classifier
    DateTime CreatedAt
);
```

---

#### PrCommentResolved
Spoke notifies hub when it has resolved a PR comment (responded, auto-fixed, or escalated).

**Signature:**
```csharp
public async Task PrCommentResolved(PrCommentResolvedEvent evt)
{
    // Spoke reports resolution of a PR comment.
}
```

**PrCommentResolvedEvent (C#):**
```csharp
public record PrCommentResolvedEvent(
    Guid SpokeId,
    string RepositoryPath,
    string PrNumber,
    string CommentId,
    string ResolutionType,                 // "auto_fix_job_created", "responded", "escalated", "positive_no_action"
    string? Notes,                         // Details of the resolution
    DateTime ResolvedAt
);
```

---

### 4.3 Hub → Spoke (Client Methods)

Spoke client receives these method invocations from the hub. Each method should be awaited and acknowledged (or throw an exception for SignalR to retry).

#### AssignJob
Hub instructs spoke to create and execute a job.

**Signature (Spoke receives):**
```csharp
public async Task AssignJob(JobAssignment assignment)
{
    // Spoke:
    // 1. Creates job record locally
    // 2. Assembles prompt from project context + memory
    // 3. Spins up worker container
    // 4. Streams output back via StreamJobOutput
    // 5. Sends job status updates via ReportJobStatusChanged
}
```

**JobAssignment (C#):**
```csharp
public record JobAssignment(
    Guid JobId,
    Guid ProjectId,
    JobType Type,
    string Context,                        // Ticket details, etc.
    JobParameters Parameters,
    bool RequireApproval,                  // If true, job stays awaiting_approval until ApproveJob received
    DateTime AssignedAt
);

public record JobParameters(
    Dictionary<string, object>? CustomFields
);
```

**JobAssignment (TypeScript):**
```typescript
interface JobAssignment {
  jobId: string;
  projectId: string;
  type: "implement" | "test" | "refactor" | "investigate" | "custom";
  context: string;
  parameters: JobParameters;
  requireApproval: boolean;
  assignedAt: string;
}

interface JobParameters {
  [key: string]: any;
}
```

---

#### ApproveJob
Hub instructs spoke to approve and start a job that was awaiting approval.

**Signature (Spoke receives):**
```csharp
public async Task ApproveJob(JobApproval approval)
{
    // Spoke transitions job from awaiting_approval → queued → running
}
```

**JobApproval (C#):**
```csharp
public record JobApproval(
    Guid JobId,
    bool Approved,                         // false = rejection
    string? ApprovalNotes,
    Dictionary<string, object>? Modifications,
    DateTime ApprovedAt
);
```

**JobApproval (TypeScript):**
```typescript
interface JobApproval {
  jobId: string;
  approved: boolean;
  approvalNotes?: string;
  modifications?: Record<string, any>;
  approvedAt: string;
}
```

---

#### CancelJob
Hub instructs spoke to cancel a job.

**Signature (Spoke receives):**
```csharp
public async Task CancelJob(JobCancellation cancellation)
{
    // Spoke:
    // 1. If job running, sends SIGTERM to container
    // 2. Waits for graceful shutdown (5s timeout)
    // 3. Sends SIGKILL if still running
    // 4. Reports job status as cancelled
}
```

**JobCancellation (C#):**
```csharp
public record JobCancellation(
    Guid JobId,
    string? Reason,
    DateTime CancelledAt
);
```

**JobCancellation (TypeScript):**
```typescript
interface JobCancellation {
  jobId: string;
  reason?: string;
  cancelledAt: string;
}
```

---

#### SendMessageToSpoke
Hub relays a user message to the spoke agent.

**Signature (Spoke receives):**
```csharp
public async Task SendMessageToSpoke(UserMessage message)
{
    // Spoke processes message via Claude API with local context,
    // then responds via SendMessageFromSpoke.
}
```

**UserMessage (C#):**
```csharp
public record UserMessage(
    Guid SpokeId,
    Guid? JobId,                           // Optional context
    string Content,
    DateTime Timestamp
);
```

**UserMessage (TypeScript):**
```typescript
interface UserMessage {
  spokeId: string;
  jobId?: string;
  content: string;
  timestamp: string;
}
```

---

#### UpdateSpokeConfig
Hub instructs spoke to update its local configuration.

**Signature (Spoke receives):**
```csharp
public async Task UpdateSpokeConfig(SpokeConfigUpdate update)
{
    // Spoke writes updated config to local config.yaml
}
```

**SpokeConfigUpdate (C#):**
```csharp
public record SpokeConfigUpdate(
    ApprovalMode? ApprovalMode,
    int? MaxConcurrentJobs,
    int? HeartbeatIntervalSeconds,
    Dictionary<string, object>? CustomSettings,
    DateTime UpdatedAt
);
```

**SpokeConfigUpdate (TypeScript):**
```typescript
interface SpokeConfigUpdate {
  approvalMode?: "plan_review" | "full_autonomy" | "custom_per_job";
  maxConcurrentJobs?: number;
  heartbeatIntervalSeconds?: number;
  customSettings?: Record<string, any>;
  updatedAt: string;
}
```

---

#### IssueDirective
Hub sends a high-level directive to the spoke (e.g., "work through sprint backlog").

**Signature (Spoke receives):**
```csharp
public async Task IssueDirective(SpokeDirective directive)
{
    // Spoke interprets directive and optionally generates multiple jobs
    // or asks for clarification via SendMessageFromSpoke.
}
```

**SpokeDirective (C#):**
```csharp
public record SpokeDirective(
    Guid SpokeId,
    string Instruction,                    // e.g., "Work through sprint backlog", "Investigate PROJ-4600"
    DirectiveScope Scope,
    Dictionary<string, object>? Parameters,
    bool RequireConfirmation,
    DateTime IssuedAt
);

public enum DirectiveScope
{
    AllProjects = 0,
    SpecificProject = 1,
    SpecificTickets = 2
}
```

**SpokeDirective (TypeScript):**
```typescript
interface SpokeDirective {
  spokeId: string;
  instruction: string;
  scope: "all_projects" | "specific_project" | "specific_tickets";
  parameters?: Record<string, any>;
  requireConfirmation: boolean;
  issuedAt: string;
}
```

---

#### ReceiveSpokeQuery
Hub meta-agent sends a query to the spoke (proxied over SignalR). Spoke receives, optionally invokes its own CC instance, and responds.

**Signature (Spoke receives):**
```csharp
public async Task ReceiveSpokeQuery(SpokeQuery query)
{
    // Spoke receives query from hub meta-agent.
    // Optionally invokes CC instance to reason about the query.
    // Responds via SendSpokeQueryResponse with correlationId.
}
```

**SpokeQuery (C#):**
```csharp
public record SpokeQuery(
    string CorrelationId,                  // Unique ID for matching request/response
    string Query,                          // The question/command from hub meta-agent
    string? Context,                       // Optional additional context
    DateTime Timestamp
);
```

**SpokeQuery (TypeScript):**
```typescript
interface SpokeQuery {
  correlationId: string;
  query: string;
  context?: string;
  timestamp: string;
}
```

---

### 4.3.1 Spoke → Hub (Query Responses)

#### SendSpokeQueryResponse
Spoke responds to a hub query.

**Signature (Spoke sends to Hub):**
```csharp
public async Task SendSpokeQueryResponse(SpokeQueryResponse response)
{
    // Spoke sends response back to hub with matching correlationId
}
```

**SpokeQueryResponse (C#):**
```csharp
public record SpokeQueryResponse(
    string CorrelationId,                  // Must match the correlationId from ReceiveSpokeQuery
    string Response,                       // The answer/result
    Dictionary<string, object>? Metadata,  // Optional metadata (state info, etc.)
    DateTime Timestamp
);
```

**SpokeQueryResponse (TypeScript):**
```typescript
interface SpokeQueryResponse {
  correlationId: string;
  response: string;
  metadata?: Record<string, any>;
  timestamp: string;
}
```

---

### 4.4 Hub → Frontend (Client Methods)

These methods are sent to all connected UI clients (Next.js app, mobile clients, etc.) to keep the dashboard in sync with real-time events.

#### SpokeConnected
A spoke has established its WebSocket connection.

**Signature (Frontend receives):**
```csharp
public async Task SpokeConnected(SpokeInfo spokeInfo)
{
    // Update UI: show spoke online, update last_seen
}
```

**SpokeInfo (C#):**
```csharp
public record SpokeInfo(
    Guid Id,
    string Name,
    SpokeStatus Status,
    int ActiveJobCount,
    string[] Capabilities,
    DateTime ConnectedAt
);
```

**SpokeInfo (TypeScript):**
```typescript
interface SpokeInfo {
  id: string;
  name: string;
  status: "online" | "offline" | "busy";
  activeJobCount: number;
  capabilities: string[];
  connectedAt: string;
}
```

---

#### SpokeDisconnected
A spoke has disconnected or timed out.

**Signature (Frontend receives):**
```csharp
public async Task SpokeDisconnected(string spokeId)
{
    // Update UI: show spoke offline
}
```

---

#### SpokeStatusUpdated
A spoke's status has changed (online → busy, etc.).

**Signature (Frontend receives):**
```csharp
public async Task SpokeStatusUpdated(SpokeStatusUpdate statusUpdate)
{
    // Update UI with new status
}
```

---

#### ProjectCreated
A new project has been created on a spoke.

**Signature (Frontend receives):**
```csharp
public async Task ProjectCreated(ProjectCreatedEvent evt)
{
    // Add to project list, update dashboard
}
```

---

#### ProjectUpdated
A project's metadata or status has changed.

**Signature (Frontend receives):**
```csharp
public async Task ProjectUpdated(ProjectUpdatedEvent evt)
{
    // Update project in UI
}
```

---

#### JobCreated
A new job has been created.

**Signature (Frontend receives):**
```csharp
public async Task JobCreated(JobCreatedEvent evt)
{
    // Add to job list, show in project detail
}
```

---

#### JobStatusChanged
A job's status has changed.

**Signature (Frontend receives):**
```csharp
public async Task JobStatusChanged(JobStatusChangedEvent evt)
{
    // Update job status, trigger notifications
}
```

---

#### JobOutputReceived
A new chunk of output from a running job.

**Signature (Frontend receives):**
```csharp
public async Task JobOutputReceived(JobOutputChunk chunk)
{
    // Stream to output viewer; UI appends to scrollable terminal
}
```

---

#### PendingActionCreated
A new item has entered the awaiting input queue (e.g., plan generated, job queued, question from spoke).

**Signature (Frontend receives):**
```csharp
public async Task PendingActionCreated(PendingActionEvent actionEvent)
{
    // Display notification; add to queue UI; update badge count
}
```

**PendingActionEvent (C#):**
```csharp
public record PendingActionEvent(
    Guid Id,
    Guid SpokeId,
    string SpokeName,
    Guid ProjectId,
    string ExternalKey,
    PendingActionGateType GateType,       // PlanReview, PreExecution, PostExecution, SpokeQuestion, PrReview
    string Summary,
    string Description,
    Dictionary<string, object> Metadata,  // Gate-specific data (plan content, branch name, question, etc.)
    DateTime CreatedAt
);

public enum PendingActionGateType
{
    PlanReview = 0,
    PreExecution = 1,
    PostExecution = 2,
    SpokeQuestion = 3,
    PrReview = 4
}
```

**PendingActionEvent (TypeScript):**
```typescript
interface PendingActionEvent {
  id: string;
  spokeId: string;
  spokeName: string;
  projectId: string;
  externalKey: string;
  gateType: "PlanReview" | "PreExecution" | "PostExecution" | "SpokeQuestion" | "PrReview";
  summary: string;
  description: string;
  metadata: Record<string, any>;
  createdAt: string;
}
```

---

#### PendingActionResolved
A pending action has been resolved (approved, rejected, responded to).

**Signature (Frontend receives):**
```csharp
public async Task PendingActionResolved(PendingActionResolvedEvent resolvedEvent)
{
    // Remove from queue UI; show confirmation; transition associated job/project
}
```

**PendingActionResolvedEvent (C#):**
```csharp
public record PendingActionResolvedEvent(
    Guid Id,
    Guid SpokeId,
    string Action,                         // "approve", "reject", "respond"
    string? Notes,
    DateTime ResolvedAt
);
```

**PendingActionResolvedEvent (TypeScript):**
```typescript
interface PendingActionResolvedEvent {
  id: string;
  spokeId: string;
  action: "approve" | "reject" | "respond";
  notes?: string;
  resolvedAt: string;
}
```

---

#### MessageReceived
A new message from a spoke or system event.

**Signature (Frontend receives):**
```csharp
public async Task MessageReceived(MessageEvent messageEvent)
{
    // Display in chat interface
}
```

**MessageEvent (C#):**
```csharp
public record MessageEvent(
    Guid MessageId,
    Guid SpokeId,
    string SpokeTitle,
    MessageDirection Direction,            // user_to_spoke, spoke_to_user, system
    string Content,
    Guid? JobId,
    Dictionary<string, object>? Metadata,
    DateTime Timestamp
);

public enum MessageDirection
{
    UserToSpoke = 0,
    SpokeToUser = 1,
    System = 2
}
```

**MessageEvent (TypeScript):**
```typescript
interface MessageEvent {
  messageId: string;
  spokeId: string;
  spokeTitle: string;
  direction: "user_to_spoke" | "spoke_to_user" | "system";
  content: string;
  jobId?: string;
  metadata?: Record<string, any>;
  timestamp: string;
}
```

---

## 4. Complete Data Transfer Objects (DTOs)

All requests and responses use these models. Define these once in the hub and auto-generate TypeScript types.

### 4.1 Core Enums

**C# Enums:**
```csharp
public enum SpokeStatus { Online = 0, Offline = 1, Busy = 2 }
public enum ProjectStatus { Planning = 0, Active = 1, Paused = 2, Completed = 3, Failed = 4 }
public enum JobStatus { Queued = 0, AwaitingApproval = 1, Running = 2, Completed = 3, Failed = 4, Cancelled = 5 }
public enum JobType { Implement = 0, Test = 1, Refactor = 2, Investigate = 3, Custom = 4 }
public enum ApprovalMode { PlanReview = 0, FullAutonomy = 1, CustomPerJob = 2 }
public enum OutputStreamType { Stdout = 0, Stderr = 1 }
public enum MessageDirection { UserToSpoke = 0, SpokeToUser = 1, System = 2 }
public enum DirectiveScope { AllProjects = 0, SpecificProject = 1, SpecificTickets = 2 }
public enum PendingActionGateType { PlanReview = 0, PreExecution = 1, PostExecution = 2, SpokeQuestion = 3, PrReview = 4 }
```

**TypeScript Enums:**
```typescript
type SpokeStatus = "online" | "offline" | "busy";
type ProjectStatus = "planning" | "active" | "paused" | "completed" | "failed";
type JobStatus = "queued" | "awaitingApproval" | "running" | "completed" | "failed" | "cancelled";
type JobType = "implement" | "test" | "refactor" | "investigate" | "custom";
type ApprovalMode = "plan_review" | "full_autonomy" | "custom_per_job";
type OutputStreamType = "stdout" | "stderr";
type MessageDirection = "user_to_spoke" | "spoke_to_user" | "system";
type DirectiveScope = "all_projects" | "specific_project" | "specific_tickets";
type PendingActionGateType = "PlanReview" | "PreExecution" | "PostExecution" | "SpokeQuestion" | "PrReview";
```

### 4.2 Pagination

**C#:**
```csharp
public record PaginatedResponse<T>(
    List<T> Items,
    int Total,
    int Limit,
    int Offset
);
```

**TypeScript:**
```typescript
interface PaginatedResponse<T> {
  items: T[];
  total: number;
  limit: number;
  offset: number;
}
```

### 4.3 Error Response

**C#:**
```csharp
public record ErrorResponse(
    ErrorDetail Error
);

public record ErrorDetail(
    string Code,
    string Message,
    int Status,
    string Timestamp,
    string CorrelationId,
    Dictionary<string, object>? Details
);
```

**TypeScript:**
```typescript
interface ErrorResponse {
  error: ErrorDetail;
}

interface ErrorDetail {
  code: string;
  message: string;
  status: number;
  timestamp: string;
  correlationId: string;
  details?: Record<string, any>;
}
```

---

## 5. Error Handling

### 5.1 Standard HTTP Error Codes

| Code | Meaning | Use Case |
|------|---------|----------|
| `400` | Bad Request | Malformed JSON, invalid enum value, missing required field |
| `401` | Unauthorized | Missing JWT, invalid JWT, token expired |
| `403` | Forbidden | User lacks permission (future: multi-tenant) |
| `404` | Not Found | Resource doesn't exist |
| `409` | Conflict | Job already in terminal state, spoke already registered, duplicate external key |
| `500` | Internal Error | Database error, unexpected exception |
| `503` | Service Unavailable | Database connection lost, spoke service down |

### 5.2 SignalR Error Handling

When a spoke method invocation fails:

1. **Hub logs the error** with correlation ID.
2. **Hub sends HubException back to spoke**: Spoke reads exception and decides whether to retry or escalate.
3. **Spoke queues the event** (if durable) for replay on reconnect.

**Recommended spoke behavior:**
```csharp
try
{
    await hubConnection.InvokeAsync("AssignJob", assignment);
}
catch (HubException ex)
{
    _logger.LogError("Hub rejected job assignment: {Message}", ex.Message);
    // Queue for retry, or ask user for approval
}
```

### 5.3 Idempotency

All spoke → hub methods should be idempotent. If a spoke sends the same event twice (due to connection glitch), the hub should recognize it and de-duplicate:

- **Heartbeat:** Use latest timestamp, ignore older duplicates.
- **StreamJobOutput:** Use `(jobId, sequence)` as unique key; ignore duplicates.
- **ProjectCreated / JobCreated:** Use `(spokeId, externalKey)` or `(jobId)` as dedup key.

---

## 5.4 API Conventions

**Enum Serialization:** All enum values in JSON payloads use `snake_case`. Example values:
- `awaiting_approval` (not `AwaitingApproval`)
- `plan_review` (not `PlanReview`)
- `post_execution` (not `PostExecution`)
- `in_progress` (not `InProgress`)
- `pre_execution` (not `PreExecution`)

This applies to all REST and SignalR responses. Enums in language-specific code (C#, TypeScript) use language conventions (PascalCase for C#, kebab-case for TypeScript), but JSON serialization always uses snake_case.

**Idempotency Header Format:** `Idempotency-Key: <UUID>`. A unique UUID per request. If the same key is sent twice, the server returns the original response (same status code, same response body) instead of creating a duplicate resource.

---

## 6. Rate Limiting & Streaming

### 6.1 Rate Limiting (Future Enhancement)

Recommended for Phase 2+ (not in v1.0):

- **Job creation:** Max 10 jobs/minute per spoke.
- **Message sends:** Max 30 messages/minute per spoke.
- **Heartbeat:** Expected every 30 seconds; allow variance up to 60 seconds.
- **Output streaming:** No hard limit; buffer and batch every 100ms or 50KB.

### 6.2 Output Streaming

**Chunk Size:** 4-16 KB per chunk (configurable).

**Buffering Strategy:**
- Spoke buffers output from worker stdout/stderr.
- Flushes to hub when:
  - Buffer reaches 50 KB, OR
  - 100ms elapsed since last flush, OR
  - Worker process terminates.
- Hub buffers chunks (up to 100 chunks or 500 KB) before persisting to database.
- Hub broadcasts immediately to subscribed UI clients.

**Guaranteed Delivery:**
- Spoke tracks sequence number per job.
- Hub acknowledges receipt of chunks (implicit via heartbeat).
- Spoke re-sends unacknowledged chunks on reconnect (up to 5 retries).

### 6.3 Heartbeat Protocol

**Interval:** 30 seconds (configurable via `SpokeConfigUpdate`).

**Timeout:** If hub doesn't receive heartbeat within 60 seconds, mark spoke offline.

**Grace Period:** Spokes can reconnect within 5 minutes of disconnection; all state is preserved.

**Auto-Reconnection:** SignalR's built-in exponential backoff handles this transparently.

### 6.4 Reconnection Protocol

On spoke reconnection:

1. Spoke re-sends `RegisterSpoke` with same identity.
2. Hub recognizes spoke ID and restores session state.
3. Spoke queries hub for any pending commands (e.g., `CancelJob`, `UpdateConfig`).
4. Spoke replays any queued events (project created, job status changes) that occurred while offline.
5. Spoke resumes any running jobs; if container crashed, spoke recreates container and resumes from checkpoint.

---

## 7. Security Considerations

### 7.1 Authentication & Authorization

- **JWT Bearer Tokens:** Standard RS256 or HS256. Include `sub` (user ID), `iat`, `exp`, `scope` claims.
- **Spoke Service Tokens:** Pre-shared or mTLS certificates. Store securely in spoke config file (permissions 0600).
- **Token Rotation:** Refresh tokens valid for 1 hour; rotate before expiry.

### 7.2 HTTPS/TLS

- All hub endpoints served over HTTPS.
- SignalR WebSocket over WSS (encrypted).
- Self-signed certs acceptable for self-hosted hub; consider Let's Encrypt via Caddy reverse proxy.

### 7.3 Data Boundaries

- **Hub stores:** Spoke metadata, project metadata, job metadata, terminal output, message text.
- **Hub never stores:** Source code, credentials, API keys, secrets.
- **Spoke stores:** Full source code, credentials, context, memory.
- **Data transmitted:** Status updates, plans, summaries, terminal output.
- **Hub UI Display:** Full code diffs are not displayed in the hub dashboard; the hub focuses on job status, plans, and summaries. The API does not filter or redact data, but the UI presentation layer controls what is shown.

### 7.4 Audit Logging

Hub logs all operations with correlation ID:
- User authentication (success/failure).
- Job creation, approval, cancellation.
- Spoke registration, disconnection.
- Config changes.

Retain logs for 90 days minimum (configurable).

---

## 8. Example Workflows

### 8.1 User Creates a Job via Hub UI

1. **User clicks "Create Job"** in project detail → UI sends `POST /api/jobs`.
2. **Hub creates job record** with status `awaiting_approval` (if approval mode enabled).
3. **Hub broadcasts `JobCreated`** to all connected clients and the target spoke via SignalR.
4. **Spoke receives `AssignJob`** → respects approval gate and waits.
5. **User reviews plan** in UI → clicks "Approve" → sends `PUT /api/jobs/{jobId}/approve`.
6. **Hub sends `ApproveJob`** via SignalR.
7. **Spoke receives `ApproveJob`** → transitions job to `queued` → `running`.
8. **Spoke spins up worker container**, streams output via `StreamJobOutput`.
9. **Hub persists output chunks**, broadcasts `JobOutputReceived` to UI clients.
10. **User views live output** in UI (SSE stream or WebSocket).
11. **Worker completes** → spoke sends `ReportJobStatusChanged` with status `completed` and summary.
12. **Hub broadcasts update** → UI updates job status and displays summary.

### 8.2 Spoke Reconnects After Network Loss

1. **Spoke loses connection** (network glitch, hibernation, etc.).
2. **SignalR's auto-reconnect** triggers after 5 seconds.
3. **Spoke connects to hub** and re-sends `RegisterSpoke`.
4. **Hub recognizes spoke ID** and restores session.
5. **Spoke sends `Heartbeat`** to confirm status.
6. **Hub returns any pending commands** (e.g., `CancelJob`, `UpdateConfig`).
7. **Spoke replays queued events** (output chunks, job status changes).
8. **User sees spoke come online** in UI dashboard.

### 8.3 User Sends a Message to a Spoke

1. **User types message** in hub UI spoke chat → sends `POST /api/spokes/{spokeId}/messages`.
2. **Hub creates message record** and broadcasts `MessageReceived` to UI and via `SendMessageToSpoke` to spoke.
3. **Spoke receives message** → passes to Claude API with local project context.
4. **Claude returns response** → spoke sends `SendMessageFromSpoke`.
5. **Hub broadcasts `MessageReceived`** to all UI clients.
6. **User sees response** in chat thread.

---

## 9. Implementation Checklist

- [ ] Define all C# record types in `Nexus.Hub.Domain/Models/`.
- [ ] Define all TypeScript types and generate via Swagger/OpenAPI.
- [ ] Implement all REST endpoints in controllers.
- [ ] Implement SignalR hub with all server/client methods.
- [ ] Add authentication: Google OAuth + JWT token exchange.
- [ ] Add error handling middleware with correlation IDs.
- [ ] Add SignalR event buffering and deduplication logic.
- [ ] Add output streaming with chunking and persistence.
- [ ] Add audit logging with retention policy.
- [ ] Integration tests for all workflows (create job, approve, stream output, reconnect).
- [ ] Load test: 10 spokes, 100 concurrent jobs, 10k messages/minute.
- [ ] Security review: token rotation, TLS, data boundaries.

---

**This contract is the single source of truth for both hub and spoke implementations. All deviations must be documented and approved.**
