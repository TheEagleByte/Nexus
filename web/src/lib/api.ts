import type {
  SpokeListResponse,
  SpokeDetailResponse,
  ProjectListResponse,
  ProjectResponse,
  JobListResponse,
  JobDetailResponse,
  JobOutputResponse,
} from "@/types/api";

const HUB_API_URL = process.env.HUB_API_URL ?? "http://localhost:5000";

async function hubFetch<T>(path: string): Promise<T> {
  const res = await fetch(`${HUB_API_URL}${path}`, {
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`Hub API error: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<T>;
}

export async function fetchSpokes(): Promise<SpokeListResponse> {
  return hubFetch<SpokeListResponse>("/api/spokes");
}

export async function fetchSpoke(id: string): Promise<SpokeDetailResponse> {
  return hubFetch<SpokeDetailResponse>(`/api/spokes/${id}`);
}

export async function fetchSpokeProjects(
  spokeId: string
): Promise<ProjectListResponse> {
  return hubFetch<ProjectListResponse>(`/api/spokes/${spokeId}/projects`);
}

export async function fetchProject(id: string): Promise<ProjectResponse> {
  return hubFetch<ProjectResponse>(`/api/projects/${id}`);
}

export async function fetchAllProjects(params?: {
  status?: string;
  limit?: number;
  offset?: number;
}): Promise<ProjectListResponse> {
  const query = new URLSearchParams();
  if (params?.status) query.set("status", params.status);
  if (params?.limit) query.set("limit", String(params.limit));
  if (params?.offset) query.set("offset", String(params.offset));
  const qs = query.toString();
  return hubFetch<ProjectListResponse>(`/api/projects${qs ? `?${qs}` : ""}`);
}

export async function fetchProjectJobs(
  projectId: string,
  params?: { status?: string; type?: string; limit?: number; offset?: number }
): Promise<JobListResponse> {
  const query = new URLSearchParams();
  if (params?.status) query.set("status", params.status);
  if (params?.type) query.set("type", params.type);
  if (params?.limit) query.set("limit", String(params.limit));
  if (params?.offset) query.set("offset", String(params.offset));
  const qs = query.toString();
  return hubFetch<JobListResponse>(
    `/api/projects/${projectId}/jobs${qs ? `?${qs}` : ""}`
  );
}

export async function fetchJob(id: string): Promise<JobDetailResponse> {
  return hubFetch<JobDetailResponse>(`/api/jobs/${id}`);
}

export async function fetchJobOutput(
  jobId: string,
  limit = 100,
  offset = 0
): Promise<JobOutputResponse> {
  return hubFetch<JobOutputResponse>(
    `/api/jobs/${jobId}/output?limit=${limit}&offset=${offset}`
  );
}

export async function fetchAllJobs(params?: {
  status?: string;
  type?: string;
  limit?: number;
  offset?: number;
}): Promise<JobListResponse> {
  const query = new URLSearchParams();
  if (params?.status) query.set("status", params.status);
  if (params?.type) query.set("type", params.type);
  if (params?.limit) query.set("limit", String(params.limit));
  if (params?.offset) query.set("offset", String(params.offset));
  const qs = query.toString();
  return hubFetch<JobListResponse>(`/api/jobs${qs ? `?${qs}` : ""}`);
}
