"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { TerminalOutput } from "@/components/jobs/terminal-output";
import { ApprovalGate } from "@/components/jobs/approval-gate";
import { cancelJob } from "@/lib/api-client";
import { useSignalR } from "@/lib/signalr";
import {
  jobStatusColor,
  jobTypeLabel,
  formatDuration,
  relativeTime,
} from "@/lib/utils";
import {
  Code2,
  TestTube2,
  RefreshCw,
  Search,
  Terminal,
  Loader2,
  XCircle,
  Copy,
  ArrowLeft,
} from "lucide-react";
import type { JobDetailResponse, JobOutputResponse, JobType } from "@/types/api";

const JOB_TYPE_ICONS: Record<JobType, typeof Code2> = {
  implement: Code2,
  test: TestTube2,
  refactor: RefreshCw,
  investigate: Search,
  custom: Terminal,
};

interface JobDetailProps {
  job: JobDetailResponse;
  initialOutput: JobOutputResponse;
}

export function JobDetail({ job, initialOutput }: JobDetailProps) {
  const router = useRouter();
  const { jobUpdates } = useSignalR();
  const [cancelling, setCancelling] = useState(false);
  const outputContentRef = useRef<() => string>(() =>
    initialOutput.chunks.map((c) => c.content).join("")
  );

  // Merge real-time status
  const jobUpdate = jobUpdates.get(job.id);
  const currentStatus = jobUpdate?.newStatus ?? job.status;

  const TypeIcon = JOB_TYPE_ICONS[job.type] ?? Terminal;
  const isCancellable =
    currentStatus === "running" ||
    currentStatus === "queued" ||
    currentStatus === "awaiting_approval";
  const isTerminal =
    currentStatus === "completed" ||
    currentStatus === "failed" ||
    currentStatus === "cancelled";

  async function handleCancel() {
    setCancelling(true);
    try {
      await cancelJob(job.id);
      toast.success("Job cancelled");
      router.refresh();
    } catch (err) {
      toast.error("Failed to cancel job", {
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setCancelling(false);
    }
  }

  function handleCopyOutput() {
    const text = outputContentRef.current();
    navigator.clipboard.writeText(text).then(
      () => toast.success("Output copied to clipboard"),
      () => toast.error("Failed to copy output")
    );
  }

  return (
    <div className="space-y-6">
      {/* Back link */}
      <Link
        href="/jobs"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ArrowLeft className="w-4 h-4" />
        All Jobs
      </Link>

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <TypeIcon className="w-6 h-6 text-muted-foreground shrink-0" />
          <div className="min-w-0">
            <h1 className="text-xl font-semibold text-foreground">
              {jobTypeLabel(job.type)} Job
            </h1>
            <span className="text-xs font-mono text-muted-foreground">
              {job.id}
            </span>
          </div>
        </div>
        <Badge
          variant="secondary"
          className={`text-sm font-mono shrink-0 ${jobStatusColor(currentStatus)}`}
        >
          {currentStatus === "running" && (
            <Loader2 className="w-3.5 h-3.5 mr-1 animate-spin" />
          )}
          {currentStatus.replace("_", " ")}
        </Badge>
      </div>

      {/* Metadata */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
        <div>
          <span className="text-xs text-muted-foreground uppercase tracking-wider block mb-1">
            Project
          </span>
          <Link
            href={`/projects/${job.projectId}`}
            className="font-mono text-foreground hover:text-primary transition-colors"
          >
            {job.projectId.slice(0, 8)}...
          </Link>
        </div>
        <div>
          <span className="text-xs text-muted-foreground uppercase tracking-wider block mb-1">
            Spoke
          </span>
          <Link
            href={`/spokes/${job.spokeId}`}
            className="font-mono text-foreground hover:text-primary transition-colors"
          >
            {job.spokeId.slice(0, 8)}...
          </Link>
        </div>
        <div>
          <span className="text-xs text-muted-foreground uppercase tracking-wider block mb-1">
            Created
          </span>
          <span className="font-mono text-foreground">
            {relativeTime(job.createdAt)}
          </span>
        </div>
        <div>
          <span className="text-xs text-muted-foreground uppercase tracking-wider block mb-1">
            Duration
          </span>
          <span className="font-mono text-foreground">
            {job.startedAt
              ? formatDuration(job.startedAt, job.completedAt)
              : "—"}
          </span>
        </div>
      </div>

      {/* Summary */}
      {job.summary && (
        <div className="rounded border border-border bg-card p-3">
          <p className="text-sm text-foreground">{job.summary}</p>
        </div>
      )}

      {/* Approval Gate */}
      {currentStatus === "awaiting_approval" && (
        <ApprovalGate
          jobId={job.id}
          outputChunks={initialOutput.chunks}
        />
      )}

      {/* Terminal Output */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            Output
          </h2>
          <div className="flex gap-2">
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={handleCopyOutput}
              title="Copy output"
            >
              <Copy className="w-3.5 h-3.5" />
            </Button>
          </div>
        </div>
        <TerminalOutput
          jobId={job.id}
          initialChunks={initialOutput.chunks}
          isComplete={isTerminal}
          onContentRef={outputContentRef}
        />
      </div>

      {/* Actions */}
      {isCancellable && (
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={handleCancel}
            disabled={cancelling}
            className="border-status-error text-status-error hover:bg-status-error/10"
          >
            {cancelling ? (
              <Loader2 className="w-4 h-4 mr-1 animate-spin" />
            ) : (
              <XCircle className="w-4 h-4 mr-1" />
            )}
            Cancel Job
          </Button>
        </div>
      )}
    </div>
  );
}
