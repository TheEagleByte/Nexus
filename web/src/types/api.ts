export type SpokeStatus = "online" | "offline" | "busy";

export type ProjectStatus =
  | "planning"
  | "active"
  | "paused"
  | "completed"
  | "failed"
  | "archived";

export type JobStatus =
  | "queued"
  | "awaiting_approval"
  | "running"
  | "completed"
  | "failed"
  | "cancelled";

export type JobType =
  | "implement"
  | "test"
  | "refactor"
  | "investigate"
  | "custom";

export interface SpokeResponse {
  id: string;
  name: string;
  status: SpokeStatus;
  lastSeen: string;
  activeJobCount: number;
  capabilities: string[];
  config: Record<string, unknown>;
}

export interface SpokeListResponse {
  spokes: SpokeResponse[];
  total: number;
  limit: number;
  offset: number;
}

export interface SpokeDetailResponse extends SpokeResponse {
  registeredAt: string;
  totalJobsCompleted: number;
  profile: SpokeProfile | null;
  resourceUsage: ResourceUsage | null;
}

export interface SpokeProfile {
  displayName: string;
  machineDescription: string;
  repos: { name: string; remoteUrl: string }[];
  jiraConfig: { instanceUrl: string; projectKeys: string[] } | null;
  integrations: string[];
  description: string;
}

export interface ResourceUsage {
  memoryUsageMb: number;
  cpuUsagePercent: number;
  diskUsageMb: number;
}

export interface ProjectResponse {
  id: string;
  spokeId: string;
  spokeName: string;
  externalKey: string | null;
  name: string;
  status: ProjectStatus;
  createdAt: string;
  updatedAt: string;
  activeJobCount: number;
  totalJobCount: number;
  summary: string | null;
}

export interface ProjectListResponse {
  projects: ProjectResponse[];
  total: number;
  limit: number;
  offset: number;
}

export interface JobResponse {
  id: string;
  projectId: string;
  spokeId: string;
  type: JobType;
  status: JobStatus;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  summary: string | null;
}

export interface JobListResponse {
  jobs: JobResponse[];
  total: number;
  limit: number;
  offset: number;
}

export interface JobDetailResponse extends JobResponse {
  outputChunkCount: number;
  outputTotalBytes: number;
  progress: JobProgress | null;
  metadata: Record<string, unknown> | null;
}

export interface JobProgress {
  elapsedSeconds: number;
  estimatedTotalSeconds: number | null;
}

export interface OutputChunk {
  sequence: number;
  content: string;
  streamType: string;
  timestamp: string;
}

export interface JobOutputResponse {
  jobId: string;
  chunks: OutputChunk[];
  totalChunks: number;
  limit: number;
  offset: number;
  isComplete: boolean;
}

export interface CreateJobRequest {
  projectId: string;
  type: JobType;
  requiresApproval: boolean;
  context?: unknown;
}

export interface ApproveJobRequest {
  approved: boolean;
  modifications?: unknown;
}

export interface CancelJobRequest {
  reason?: string;
}

export interface JobOutputReceivedEvent {
  jobId: string;
  spokeId: string;
  sequence: number;
  content: string;
  streamType: string;
  timestamp: string;
}

export interface ProjectUpdatedEvent {
  projectId: string;
  status: ProjectStatus;
  timestamp: string;
}

export interface JobStatusChangedEvent {
  spokeId: string;
  jobId: string;
  newStatus: JobStatus;
  previousStatus: JobStatus;
  summary?: string;
  timestamp: string;
}

// Conversations

export type ConversationRole = "user" | "assistant";

export interface ConversationSummary {
  id: string;
  spokeId: string | null;
  spokeName: string | null;
  title: string;
  createdAt: string;
  updatedAt: string;
  ccSessionId: string | null;
  messageCount: number;
}

export interface ConversationMessage {
  id: string;
  conversationId: string;
  role: ConversationRole;
  content: string;
  timestamp: string;
}

export interface ConversationDetail extends ConversationSummary {
  messages: ConversationMessage[];
}

export interface ConversationListResponse {
  conversations: ConversationSummary[];
  total: number;
  limit: number;
  offset: number;
}

export interface ConversationMessageReceivedEvent {
  conversationId: string;
  messageId: string;
  role: ConversationRole;
  content: string;
  timestamp: string;
  streaming: boolean;
}

export interface CreateConversationRequest {
  spokeId?: string | null;
  title: string;
}

export interface SendConversationMessageRequest {
  content: string;
}
