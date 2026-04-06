"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { JobTimeline } from "@/components/jobs/job-timeline";
import { CreateJobDialog } from "@/components/jobs/create-job-dialog";
import { fetchProjectJobsClient } from "@/lib/api-client";
import { useSignalR } from "@/lib/signalr";
import {
  projectStatusColor,
  jobTypeLabel,
  relativeTime,
} from "@/lib/utils";
import { ArrowRight, ExternalLink, Loader2 } from "lucide-react";
import type { ProjectResponse, JobResponse, JobStatus, JobType } from "@/types/api";

const ALL_STATUSES: JobStatus[] = [
  "queued",
  "awaiting_approval",
  "running",
  "completed",
  "failed",
  "cancelled",
];

const ALL_TYPES: JobType[] = [
  "implement",
  "test",
  "refactor",
  "investigate",
  "custom",
];

interface ProjectDetailProps {
  project: ProjectResponse;
  initialJobs: JobResponse[];
  totalJobs: number;
}

export function ProjectDetail({ project, initialJobs, totalJobs }: ProjectDetailProps) {
  const { projectUpdates, jobUpdates } = useSignalR();
  const [jobs, setJobs] = useState<JobResponse[]>(initialJobs);
  const [total, setTotal] = useState(totalJobs);
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [typeFilter, setTypeFilter] = useState<string>("all");
  const [loadingMore, setLoadingMore] = useState(false);

  // Merge real-time project status
  const projectUpdate = projectUpdates.get(project.id);
  const currentStatus = projectUpdate?.status ?? project.status;

  // Merge real-time job status updates
  const mergedJobs = jobs.map((job) => {
    const update = jobUpdates.get(job.id);
    if (update) {
      return { ...job, status: update.newStatus, summary: update.summary ?? job.summary };
    }
    return job;
  });

  // Client-side filtering
  const filteredJobs = mergedJobs.filter((job) => {
    if (statusFilter !== "all" && job.status !== statusFilter) return false;
    if (typeFilter !== "all" && job.type !== typeFilter) return false;
    return true;
  });

  const hasMore = jobs.length < total;

  async function handleLoadMore() {
    setLoadingMore(true);
    try {
      const data = await fetchProjectJobsClient(project.id, {
        limit: 20,
        offset: jobs.length,
        ...(statusFilter !== "all" && { status: statusFilter }),
        ...(typeFilter !== "all" && { type: typeFilter }),
      });
      setJobs((prev) => [...prev, ...data.jobs]);
      setTotal(data.total);
    } catch {
      toast.error("Failed to load more jobs");
    } finally {
      setLoadingMore(false);
    }
  }

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

      {/* Job History */}
      <div className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            Job History
          </h2>
          <div className="flex gap-2">
            <Select value={statusFilter} onValueChange={(v) => { if (v) setStatusFilter(v); }}>
              <SelectTrigger className="h-7 w-full sm:w-[140px] text-xs">
                <SelectValue>
                  {statusFilter === "all" ? "All statuses" : statusFilter.replace("_", " ")}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {ALL_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {s.replace("_", " ")}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={typeFilter} onValueChange={(v) => { if (v) setTypeFilter(v); }}>
              <SelectTrigger className="h-7 w-full sm:w-[130px] text-xs">
                <SelectValue>
                  {typeFilter === "all" ? "All types" : jobTypeLabel(typeFilter as JobType)}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {ALL_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {jobTypeLabel(t)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <JobTimeline jobs={filteredJobs} />

        {hasMore && (
          <div className="text-center pt-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleLoadMore}
              disabled={loadingMore}
            >
              {loadingMore && <Loader2 className="w-4 h-4 mr-1 animate-spin" />}
              Load More
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
