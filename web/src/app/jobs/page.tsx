import { fetchAllJobs, fetchSpokes } from "@/lib/api";
import { JobQueue } from "./job-queue";
import type { JobResponse, SpokeResponse } from "@/types/api";

function first(v: string | string[] | undefined): string | undefined {
  return Array.isArray(v) ? v[0] : v;
}

export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;

  const status = first(params.status);
  const type = first(params.type);
  const limit = Math.max(1, Math.min(Number(first(params.limit)) || 20, 100));
  const offset = Math.max(Number(first(params.offset)) || 0, 0);

  let jobs: JobResponse[] = [];
  let total = 0;
  try {
    const data = await fetchAllJobs({ status, type, limit, offset });
    jobs = data.jobs;
    total = data.total;
  } catch {
    // API unavailable — show empty state
  }

  let spokes: SpokeResponse[] = [];
  try {
    const data = await fetchSpokes();
    spokes = data.spokes;
  } catch {
    // Continue without spokes for filter
  }

  return (
    <JobQueue
      initialJobs={jobs}
      total={total}
      spokes={spokes}
      filters={{ status, type, limit, offset }}
    />
  );
}
