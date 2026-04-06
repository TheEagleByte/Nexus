import type {
  CreateJobRequest,
  ApproveJobRequest,
  CancelJobRequest,
  JobResponse,
  JobType,
  JobListResponse,
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

export async function retryJob(
  projectId: string,
  type: JobType
): Promise<JobResponse> {
  return createJob({ projectId, type, requiresApproval: false });
}

export async function fetchProjectJobsClient(
  projectId: string,
  params?: { status?: string; type?: string; limit?: number; offset?: number }
): Promise<JobListResponse> {
  const query = new URLSearchParams();
  if (params?.status) query.set("status", params.status);
  if (params?.type) query.set("type", params.type);
  if (params?.limit) query.set("limit", String(params.limit));
  if (params?.offset) query.set("offset", String(params.offset));
  const qs = query.toString();
  const res = await fetch(
    `${HUB_API_URL}/api/projects/${projectId}/jobs${qs ? `?${qs}` : ""}`,
    { cache: "no-store" }
  );
  if (!res.ok) {
    throw new Error(`Hub API error: ${res.status}`);
  }
  return res.json() as Promise<JobListResponse>;
}
