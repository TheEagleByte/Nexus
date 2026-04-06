import type {
  CreateJobRequest,
  ApproveJobRequest,
  CancelJobRequest,
  JobResponse,
  CreateConversationRequest,
  ConversationSummary,
  SendConversationMessageRequest,
  ConversationMessage,
} from "@/types/api";

const HUB_API_URL =
  process.env.NEXT_PUBLIC_HUB_API_URL ?? "http://localhost:5000";

async function hubMutate<T>(
  path: string,
  method = "POST",
  body?: unknown
): Promise<T> {
  const res = await fetch(`${HUB_API_URL}${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`Hub API error: ${res.status} ${text}`);
  }

  const contentType = res.headers.get("content-type");
  if (contentType?.includes("application/json")) {
    return res.json() as Promise<T>;
  }
  return undefined as T;
}

export async function createJob(req: CreateJobRequest): Promise<JobResponse> {
  return hubMutate<JobResponse>("/api/jobs", "POST", req);
}

export async function approveJob(
  jobId: string,
  req: ApproveJobRequest
): Promise<void> {
  return hubMutate<void>(`/api/jobs/${jobId}/approve`, "POST", req);
}

export async function cancelJob(
  jobId: string,
  req?: CancelJobRequest
): Promise<void> {
  return hubMutate<void>(`/api/jobs/${jobId}/cancel`, "POST", req ?? {});
}

export async function createConversation(
  req: CreateConversationRequest
): Promise<ConversationSummary> {
  return hubMutate<ConversationSummary>("/api/conversations", "POST", req);
}

export async function sendConversationMessage(
  conversationId: string,
  req: SendConversationMessageRequest
): Promise<ConversationMessage> {
  return hubMutate<ConversationMessage>(
    `/api/conversations/${conversationId}/messages`,
    "POST",
    req
  );
}

export async function archiveConversation(
  conversationId: string
): Promise<void> {
  return hubMutate<void>(`/api/conversations/${conversationId}`, "DELETE");
}
