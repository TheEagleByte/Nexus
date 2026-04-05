import type {
  SpokeListResponse,
  SpokeDetailResponse,
  ProjectListResponse,
  JobListResponse,
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

export async function fetchProjectJobs(
  projectId: string
): Promise<JobListResponse> {
  return hubFetch<JobListResponse>(`/api/projects/${projectId}/jobs`);
}
