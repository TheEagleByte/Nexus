import { fetchAllJobs, fetchSpokes } from "@/lib/api";
import { JobQueue } from "./job-queue";
import type { JobResponse, SpokeResponse } from "@/types/api";

export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const params = await searchParams;

  const status = params.status;
  const type = params.type;
  const limit = Math.min(Number(params.limit) || 20, 100);
  const offset = Math.max(Number(params.offset) || 0, 0);

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
