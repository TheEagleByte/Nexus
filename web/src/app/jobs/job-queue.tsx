"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useCallback } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useSignalR } from "@/lib/signalr";
import {
  jobStatusColor,
  jobTypeLabel,
  relativeTime,
  formatDuration,
} from "@/lib/utils";
import {
  Code2,
  TestTube2,
  RefreshCw,
  Search,
  Terminal,
  Loader2,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import type { JobResponse, JobType, SpokeResponse } from "@/types/api";

const JOB_TYPE_ICONS: Record<JobType, typeof Code2> = {
  implement: Code2,
  test: TestTube2,
  refactor: RefreshCw,
  investigate: Search,
  custom: Terminal,
};

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "all", label: "All Statuses" },
  { value: "queued", label: "Queued" },
  { value: "awaiting_approval", label: "Awaiting Approval" },
  { value: "running", label: "Running" },
  { value: "completed", label: "Completed" },
  { value: "failed", label: "Failed" },
  { value: "cancelled", label: "Cancelled" },
];

const TYPE_OPTIONS: { value: string; label: string }[] = [
  { value: "all", label: "All Types" },
  { value: "implement", label: "Implement" },
  { value: "test", label: "Test" },
  { value: "refactor", label: "Refactor" },
  { value: "investigate", label: "Investigate" },
];

interface JobQueueProps {
  initialJobs: JobResponse[];
  total: number;
  spokes: SpokeResponse[];
  filters: {
    status?: string;
    type?: string;
    limit: number;
    offset: number;
  };
}

export function JobQueue({ initialJobs, total, spokes, filters }: JobQueueProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { jobUpdates } = useSignalR();

  // Merge real-time status updates into initial jobs
  const jobs = initialJobs.map((job) => {
    const update = jobUpdates.get(job.id);
    if (update) {
      return { ...job, status: update.newStatus };
    }
    return job;
  });

  const spokeNameMap = new Map(spokes.map((s) => [s.id, s.name]));
  const currentPage = Math.floor(filters.offset / filters.limit);
  const totalPages = Math.ceil(total / filters.limit);

  const updateFilter = useCallback(
    (key: string, value: string) => {
      const params = new URLSearchParams(searchParams.toString());
      if (value && value !== "all") {
        params.set(key, value);
      } else {
        params.delete(key);
      }
      // Reset offset when changing filters
      if (key !== "offset") {
        params.delete("offset");
      }
      router.push(`/jobs?${params.toString()}`);
    },
    [router, searchParams]
  );

  const goToPage = useCallback(
    (page: number) => {
      updateFilter("offset", String(page * filters.limit));
    },
    [updateFilter, filters.limit]
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Jobs</h1>
        <span className="text-sm font-mono text-muted-foreground">
          {total} total
        </span>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <Select
          value={filters.status ?? "all"}
          onValueChange={(v) => updateFilter("status", v ?? "all")}
        >
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STATUS_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={filters.type ?? "all"}
          onValueChange={(v) => updateFilter("type", v ?? "all")}
        >
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TYPE_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Job list */}
      {jobs.length > 0 ? (
        <div className="space-y-2">
          {jobs.map((job) => {
            const TypeIcon = JOB_TYPE_ICONS[job.type] ?? Terminal;
            const spokeName = spokeNameMap.get(job.spokeId) ?? job.spokeId.slice(0, 8);

            return (
              <Link
                key={job.id}
                href={`/jobs/${job.id}`}
                className="flex items-center gap-4 rounded border border-border bg-card p-3 transition-colors hover:border-primary/50"
              >
                {/* Type icon */}
                <TypeIcon className="w-4 h-4 text-muted-foreground shrink-0" />

                {/* Main info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-foreground">
                      {jobTypeLabel(job.type)}
                    </span>
                    {job.summary && (
                      <span className="text-xs text-muted-foreground truncate">
                        &mdash; {job.summary}
                      </span>
                    )}
                  </div>
                  <div className="flex gap-3 text-xs text-muted-foreground font-mono mt-0.5">
                    <span>{spokeName}</span>
                    <span>{relativeTime(job.createdAt)}</span>
                    {job.startedAt && (
                      <span>{formatDuration(job.startedAt, job.completedAt)}</span>
                    )}
                  </div>
                </div>

                {/* Status badge */}
                <Badge
                  variant="secondary"
                  className={`text-xs font-mono rounded-full shrink-0 ${jobStatusColor(job.status)}`}
                >
                  {job.status === "running" && (
                    <Loader2 className="w-3 h-3 mr-1 animate-spin" />
                  )}
                  {job.status.replace("_", " ")}
                </Badge>
              </Link>
            );
          })}
        </div>
      ) : (
        <div className="rounded-md border border-border bg-card p-8 text-center">
          <p className="text-sm text-muted-foreground">
            {filters.status || filters.type
              ? "No jobs match the current filters."
              : "No jobs yet."}
          </p>
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => goToPage(currentPage - 1)}
            disabled={currentPage === 0}
          >
            <ChevronLeft className="w-4 h-4" />
          </Button>
          <span className="text-sm font-mono text-muted-foreground px-3">
            {currentPage + 1} / {totalPages}
          </span>
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => goToPage(currentPage + 1)}
            disabled={currentPage >= totalPages - 1}
          >
            <ChevronRight className="w-4 h-4" />
          </Button>
        </div>
      )}
    </div>
  );
}
