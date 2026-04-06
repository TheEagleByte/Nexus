"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { approveJob } from "@/lib/api-client";
import { stripAnsi } from "@/lib/utils";
import { Check, X, Loader2, ShieldCheck } from "lucide-react";
import type { OutputChunk } from "@/types/api";

interface ApprovalGateProps {
  jobId: string;
  outputChunks: OutputChunk[];
  onAction?: () => void;
}

export function ApprovalGate({
  jobId,
  outputChunks,
  onAction,
}: ApprovalGateProps) {
  const router = useRouter();
  const [approving, setApproving] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  const [showRejectDialog, setShowRejectDialog] = useState(false);
  const [feedback, setFeedback] = useState("");

  const planText = outputChunks
    .map((c) => stripAnsi(c.content))
    .join("");

  async function handleApprove() {
    setApproving(true);
    try {
      await approveJob(jobId, { approved: true });
      toast.success("Job approved", {
        description: "The agent will proceed with execution.",
      });
      onAction?.();
      router.refresh();
    } catch (err) {
      toast.error("Failed to approve job", {
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setApproving(false);
    }
  }

  async function handleReject() {
    setRejecting(true);
    try {
      await approveJob(jobId, {
        approved: false,
        modifications: feedback.trim() ? { feedback: feedback.trim() } : undefined,
      });
      toast.success("Changes requested", {
        description: "Feedback sent to the agent.",
      });
      setShowRejectDialog(false);
      setFeedback("");
      onAction?.();
      router.refresh();
    } catch (err) {
      toast.error("Failed to request changes", {
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setRejecting(false);
    }
  }

  return (
    <div className="rounded-md border-l-2 border-primary bg-surface p-4 space-y-4">
      {/* Header */}
      <div className="flex items-center gap-2">
        <ShieldCheck className="w-5 h-5 text-primary" />
        <h3 className="text-sm font-semibold text-foreground">
          Awaiting Approval
        </h3>
      </div>

      {/* Plan content */}
      {planText.trim() ? (
        <div className="rounded border border-border bg-background p-3 max-h-[400px] overflow-y-auto">
          <pre className="font-mono text-xs text-foreground whitespace-pre-wrap break-words">
            {planText}
          </pre>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          No plan output received yet. The agent may still be generating its plan.
        </p>
      )}

      {/* Actions */}
      <div className="flex gap-2">
        <Button
          onClick={handleApprove}
          disabled={approving || rejecting}
          size="sm"
          className="bg-status-success hover:bg-status-success/90 text-background"
        >
          {approving ? (
            <Loader2 className="w-4 h-4 mr-1 animate-spin" />
          ) : (
            <Check className="w-4 h-4 mr-1" />
          )}
          Approve
        </Button>
        <Button
          onClick={() => setShowRejectDialog(true)}
          disabled={approving || rejecting}
          size="sm"
          variant="outline"
          className="border-status-error text-status-error hover:bg-status-error/10"
        >
          <X className="w-4 h-4 mr-1" />
          Request Changes
        </Button>
      </div>

      {/* Reject dialog */}
      <Dialog open={showRejectDialog} onOpenChange={setShowRejectDialog}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Request Changes</DialogTitle>
            <DialogDescription>
              Provide feedback for the agent to revise its plan.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label>Feedback</Label>
            <Textarea
              placeholder="What should the agent change..."
              value={feedback}
              onChange={(e) => setFeedback(e.target.value)}
              rows={4}
              className="font-mono text-sm"
              autoFocus
            />
          </div>
          <DialogFooter>
            <Button
              onClick={handleReject}
              disabled={rejecting || !feedback.trim()}
              size="sm"
              variant="destructive"
            >
              {rejecting && <Loader2 className="w-4 h-4 mr-1 animate-spin" />}
              Send Feedback
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
