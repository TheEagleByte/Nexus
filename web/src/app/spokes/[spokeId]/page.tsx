import { notFound } from "next/navigation";
import { fetchSpoke, fetchSpokeProjects, fetchProjectJobs } from "@/lib/api";
import { SpokeHeader } from "@/components/spoke-detail/spoke-header";
import { ProjectsList } from "@/components/spoke-detail/projects-list";
import { JobsList } from "@/components/spoke-detail/jobs-list";
import { SpokeDetailTabs } from "./spoke-detail-tabs";
import type { JobResponse, ProjectResponse, SpokeDetailResponse } from "@/types/api";

export default async function SpokeDetailPage({
  params,
}: {
  params: Promise<{ spokeId: string }>;
}) {
  const { spokeId } = await params;

  let spoke: SpokeDetailResponse;
  try {
    spoke = await fetchSpoke(spokeId);
  } catch {
    notFound();
  }

  let projects: ProjectResponse[] = [];
  try {
    const data = await fetchSpokeProjects(spokeId);
    projects = data.projects;
  } catch {
    // Projects fetch failed — continue with empty list
  }

  // Fetch recent jobs from all projects
  let recentJobs: JobResponse[] = [];
  try {
    const jobResults = await Promise.all(
      projects.slice(0, 5).map((p) => fetchProjectJobs(p.id).catch(() => null))
    );
    recentJobs = jobResults
      .filter((r): r is NonNullable<typeof r> => r !== null)
      .flatMap((r) => r.jobs)
      .sort(
        (a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      )
      .slice(0, 10);
  } catch {
    // Jobs are non-critical, continue without them
  }

  return (
    <div className="-m-4 sm:-m-6 flex flex-col min-h-[calc(100vh-3.5rem)]">
      <SpokeHeader spoke={spoke} />

      {/* Desktop: side-by-side layout */}
      <div className="hidden lg:flex flex-1">
        {/* Main area — conversation placeholder */}
        <div className="flex-1 p-6">
          <div className="rounded-md border border-border bg-card p-8 text-center">
            <div className="text-2xl mb-2 font-mono text-muted-foreground">
              {">"}_
            </div>
            <p className="text-sm text-muted-foreground">
              Conversation panel coming soon
            </p>
          </div>
        </div>

        {/* Right sidebar */}
        <div className="w-72 border-l border-border bg-surface overflow-y-auto">
          <div className="border-b border-border">
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider p-4 pb-0 mb-2">
              Active Jobs
            </h3>
            <JobsList jobs={recentJobs.filter((j) => j.status === "running" || j.status === "queued" || j.status === "awaiting_approval")} />
          </div>
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider p-4 pb-0 mb-2">
              Projects
            </h3>
            <ProjectsList projects={projects} />
          </div>
        </div>
      </div>

      {/* Mobile: tabbed layout */}
      <div className="lg:hidden">
        <SpokeDetailTabs projects={projects} jobs={recentJobs} />
      </div>
    </div>
  );
}
