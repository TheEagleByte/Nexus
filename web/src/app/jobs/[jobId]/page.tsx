import { notFound } from "next/navigation";
import { fetchJob, fetchJobOutput } from "@/lib/api";
import { JobDetail } from "./job-detail";
import type { JobDetailResponse, JobOutputResponse } from "@/types/api";

export default async function JobDetailPage({
  params,
}: {
  params: Promise<{ jobId: string }>;
}) {
  const { jobId } = await params;

  let job: JobDetailResponse;
  try {
    job = await fetchJob(jobId);
  } catch {
    notFound();
  }

  let output: JobOutputResponse = {
    jobId,
    chunks: [],
    totalChunks: 0,
    limit: 200,
    offset: 0,
    isComplete: false,
  };

  try {
    output = await fetchJobOutput(jobId, 100, 0);
  } catch {
    // Output fetch failed — continue with empty
  }

  return <JobDetail job={job} initialOutput={output} />;
}
