import { notFound } from "next/navigation";
import { fetchProject, fetchProjectJobs } from "@/lib/api";
import { ProjectDetail } from "./project-detail";
import type { ProjectResponse, JobResponse } from "@/types/api";

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

  let jobs: JobResponse[] = [];
  try {
    const data = await fetchProjectJobs(projectId);
    jobs = data.jobs;
  } catch {
    // Jobs fetch failed — continue with empty list
  }

  return <ProjectDetail project={project} initialJobs={jobs} />;
}
