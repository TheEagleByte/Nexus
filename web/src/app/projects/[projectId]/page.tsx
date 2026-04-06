import { notFound } from "next/navigation";
import { fetchProject, fetchProjectJobs } from "@/lib/api";
import { ProjectDetail } from "./project-detail";
import type { ProjectResponse, JobListResponse } from "@/types/api";

export default async function ProjectDetailPage({
  params,
}: {
  params: Promise<{ projectId: string }>;
}) {
  const { projectId } = await params;

  let project: ProjectResponse;
  try {
    project = await fetchProject(projectId);
  } catch {
    notFound();
  }

  let jobData: JobListResponse = { jobs: [], total: 0, limit: 20, offset: 0 };
  try {
    jobData = await fetchProjectJobs(projectId, { limit: 20 });
  } catch {
    // Jobs fetch failed — continue with empty list
  }

  return (
    <ProjectDetail
      project={project}
      initialJobs={jobData.jobs}
      totalJobs={jobData.total}
    />
  );
}
