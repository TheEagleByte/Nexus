"use client";

import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { JobsList } from "@/components/spoke-detail/jobs-list";
import { CreateJobDialog } from "@/components/jobs/create-job-dialog";
import { useSignalR } from "@/lib/signalr";
import {
  projectStatusColor,
  relativeTime,
} from "@/lib/utils";
import { ArrowRight, ExternalLink } from "lucide-react";
import type { ProjectResponse, JobResponse } from "@/types/api";

interface ProjectDetailProps {
  project: ProjectResponse;
  initialJobs: JobResponse[];
}

export function ProjectDetail({ project, initialJobs }: ProjectDetailProps) {
  const { projectUpdates, jobUpdates } = useSignalR();

  // Merge real-time project status
  const projectUpdate = projectUpdates.get(project.id);
  const currentStatus = projectUpdate?.status ?? project.status;

  // Merge real-time job status updates
  const jobs = initialJobs.map((job) => {
    const update = jobUpdates.get(job.id);
    if (update) {
      return { ...job, status: update.newStatus };
    }
    return job;
  });

  const activeJobs = jobs.filter(
    (j) =>
      j.status === "running" ||
      j.status === "queued" ||
      j.status === "awaiting_approval"
  );
  const completedJobs = jobs.filter(
    (j) =>
      j.status === "completed" ||
      j.status === "failed" ||
      j.status === "cancelled"
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1 min-w-0">
            <h1 className="text-2xl font-semibold text-foreground truncate">
              {project.name}
            </h1>
            <div className="flex items-center gap-3 text-sm text-muted-foreground">
              {project.externalKey && (
                <span className="font-mono">{project.externalKey}</span>
              )}
              <Link
                href={`/spokes/${project.spokeId}`}
                className="hover:text-primary transition-colors"
              >
                {project.spokeName}
                <ExternalLink className="w-3 h-3 inline ml-1" />
              </Link>
            </div>
          </div>
          <Badge
            variant="secondary"
            className={`text-sm font-mono shrink-0 ${projectStatusColor(currentStatus)}`}
          >
            {currentStatus}
          </Badge>
        </div>

        {project.summary && (
          <p className="mt-3 text-sm text-muted-foreground">
            {project.summary}
          </p>
        )}
      </div>

      {/* Stats */}
      <div className="flex gap-6 text-sm font-mono text-muted-foreground">
        <span>
          {project.activeJobCount} active / {project.totalJobCount} total jobs
        </span>
        <span>Created {relativeTime(project.createdAt)}</span>
        <span>Updated {relativeTime(project.updatedAt)}</span>
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <CreateJobDialog
          projectId={project.id}
          projectName={project.name}
        />
        <Link
          href="/jobs"
          className="inline-flex items-center gap-1 h-7 px-2.5 text-[0.8rem] font-medium rounded-[min(var(--radius-md),12px)] border border-input bg-transparent cursor-pointer hover:bg-muted hover:text-foreground transition-all dark:border-input dark:bg-input/30 dark:hover:bg-input/50"
        >
          All Jobs
          <ArrowRight className="w-3.5 h-3.5" />
        </Link>
      </div>

      {/* Active Jobs */}
      {activeJobs.length > 0 && (
        <div>
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
            Active Jobs
          </h2>
          <JobsList jobs={activeJobs} />
        </div>
      )}

      {/* Completed Jobs */}
      {completedJobs.length > 0 && (
        <div>
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
            Recent Jobs
          </h2>
          <JobsList jobs={completedJobs} />
        </div>
      )}

      {jobs.length === 0 && (
        <div className="rounded-md border border-border bg-card p-8 text-center">
          <p className="text-sm text-muted-foreground">
            No jobs yet. Create one to get started.
          </p>
        </div>
      )}
    </div>
  );
}
