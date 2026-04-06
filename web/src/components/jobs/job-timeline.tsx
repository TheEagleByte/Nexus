"use client";

import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import {
  jobStatusColor,
  jobTypeLabel,
  formatDuration,
  relativeTime,
} from "@/lib/utils";
import { Loader2 } from "lucide-react";
import type { JobResponse } from "@/types/api";

interface JobTimelineProps {
  jobs: JobResponse[];
}

function dateLabel(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const yesterday = new Date(today.getTime() - 86400000);
  const jobDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());

  if (jobDate.getTime() === today.getTime()) return "Today";
  if (jobDate.getTime() === yesterday.getTime()) return "Yesterday";
  return date.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

function statusDotColor(status: string): string {
  switch (status) {
    case "queued":
      return "bg-status-info";
    case "awaiting_approval":
      return "bg-[#00d9ff]";
    case "running":
      return "bg-status-warning";
    case "completed":
      return "bg-status-success";
    case "failed":
      return "bg-status-error";
    case "cancelled":
      return "bg-muted-foreground";
    default:
      return "bg-muted-foreground";
  }
}

export function JobTimeline({ jobs }: JobTimelineProps) {
  if (jobs.length === 0) {
    return (
      <div className="rounded-md border border-border bg-card p-8 text-center">
        <p className="text-sm text-muted-foreground">
          No jobs match the current filters.
        </p>
      </div>
    );
  }

  // Group jobs by date (keyed by ISO date for stability across years)
  const groups: { key: string; label: string; jobs: JobResponse[] }[] = [];
  let currentKey = "";
  for (const job of jobs) {
    const date = new Date(job.createdAt);
    const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
    if (key !== currentKey) {
      groups.push({ key, label: dateLabel(job.createdAt), jobs: [job] });
      currentKey = key;
    } else {
      groups[groups.length - 1].jobs.push(job);
    }
  }

  return (
    <div className="space-y-4">
      {groups.map((group) => (
        <div key={group.key}>
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
            {group.label}
          </h3>
          <div className="relative pl-5">
            {/* Timeline line */}
            <div className="absolute left-[7px] top-2 bottom-2 w-px bg-border" />

            <div className="space-y-2">
              {group.jobs.map((job) => (
                <Link
                  key={job.id}
                  href={`/jobs/${job.id}`}
                  className="relative block rounded border border-border bg-background p-3 transition-colors hover:border-primary/50"
                >
                  {/* Status dot */}
                  <div
                    className={`absolute -left-5 top-4 w-2 h-2 rounded-full ring-2 ring-background ${statusDotColor(job.status)}`}
                  />

                  <div className="flex items-center justify-between mb-1">
                    <span className="text-sm font-medium text-foreground">
                      {jobTypeLabel(job.type)}
                    </span>
                    <Badge
                      variant="secondary"
                      className={`text-xs font-mono rounded-full ${jobStatusColor(job.status)}`}
                    >
                      {job.status === "running" && (
                        <Loader2 className="w-3 h-3 mr-1 animate-spin" />
                      )}
                      {job.status.replace("_", " ")}
                    </Badge>
                  </div>

                  {job.summary && (
                    <p className="text-xs text-muted-foreground mb-1 line-clamp-1">
                      {job.summary}
                    </p>
                  )}

                  <div className="flex items-center gap-3 text-xs text-muted-foreground font-mono">
                    <span>{relativeTime(job.createdAt)}</span>
                    {job.startedAt && (
                      <span>{formatDuration(job.startedAt, job.completedAt)}</span>
                    )}
                  </div>
                </Link>
              ))}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
