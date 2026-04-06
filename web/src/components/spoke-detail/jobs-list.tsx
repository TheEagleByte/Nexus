import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { jobStatusColor, relativeTime } from "@/lib/utils";
import { Loader2 } from "lucide-react";
import type { JobResponse } from "@/types/api";

interface JobsListProps {
  jobs: JobResponse[];
}

export function JobsList({ jobs }: JobsListProps) {
  if (jobs.length === 0) {
    return (
      <div className="p-4 text-sm text-muted-foreground">
        No recent jobs
      </div>
    );
  }

  return (
    <div className="space-y-2 p-4">
      {jobs.map((job) => (
        <Link
          key={job.id}
          href={`/jobs/${job.id}`}
          className="block rounded border border-border bg-background p-3 transition-colors hover:border-primary/50"
        >
          <div className="flex items-center justify-between mb-1">
            <span className="text-sm font-medium text-foreground capitalize">
              {job.type}
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
            <p className="text-xs text-muted-foreground mb-1 line-clamp-2">
              {job.summary}
            </p>
          )}
          <div className="text-xs text-muted-foreground font-mono">
            Created {relativeTime(job.createdAt)}
          </div>
        </Link>
      ))}
    </div>
  );
}
