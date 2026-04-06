"use client";

import { useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Button } from "@/components/ui/button";
import { createJob } from "@/lib/api-client";
import { jobTypeLabel } from "@/lib/utils";
import { Loader2, Plus } from "lucide-react";
import type { JobType } from "@/types/api";

const JOB_TYPES: JobType[] = ["implement", "test", "refactor", "investigate"];

interface CreateJobDialogProps {
  projectId: string;
  projectName: string;
  trigger?: ReactNode;
  onSuccess?: () => void;
}

export function CreateJobDialog({
  projectId,
  projectName,
  trigger,
  onSuccess,
}: CreateJobDialogProps) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [jobType, setJobType] = useState<JobType>("implement");
  const [requiresApproval, setRequiresApproval] = useState(true);
  const [context, setContext] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    try {
      const job = await createJob({
        projectId,
        type: jobType,
        requiresApproval,
        context: context.trim() || undefined,
      });
      toast.success(`Job created: ${jobTypeLabel(jobType)}`, {
        description: `Job ${job.id.slice(0, 8)} is now ${job.status.replace("_", " ")}`,
      });
      setOpen(false);
      setContext("");
      onSuccess?.();
      router.refresh();
    } catch (err) {
      toast.error("Failed to create job", {
        description: err instanceof Error ? err.message : "Unknown error",
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          trigger ? (
            <>{trigger}</>
          ) : (
            <Button size="sm">
              <Plus className="w-4 h-4 mr-1" />
              Create Job
            </Button>
          )
        }
      />
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create Job</DialogTitle>
          <DialogDescription>
            Create a new job for{" "}
            <span className="font-mono text-foreground">{projectName}</span>
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Job Type */}
          <div className="space-y-2">
            <Label>Job Type</Label>
            <Select value={jobType} onValueChange={(v) => { if (v) setJobType(v as JobType); }}>
              <SelectTrigger className="w-full">
                <SelectValue>{jobTypeLabel(jobType)}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {JOB_TYPES.map((type) => (
                  <SelectItem key={type} value={type}>
                    {jobTypeLabel(type)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Requires Approval */}
          <div className="flex items-center gap-3">
            <button
              type="button"
              role="switch"
              aria-checked={requiresApproval}
              onClick={() => setRequiresApproval(!requiresApproval)}
              className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                requiresApproval ? "bg-primary" : "bg-input"
              }`}
            >
              <span
                className={`pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 transition-transform ${
                  requiresApproval ? "translate-x-4" : "translate-x-0"
                }`}
              />
            </button>
            <Label className="cursor-pointer" onClick={() => setRequiresApproval(!requiresApproval)}>
              Require approval before execution
            </Label>
          </div>

          {/* Context */}
          <div className="space-y-2">
            <Label>Context (optional)</Label>
            <Textarea
              placeholder="Additional instructions or context for the agent..."
              value={context}
              onChange={(e) => setContext(e.target.value)}
              rows={3}
              className="font-mono text-sm"
            />
          </div>
        </div>

        <DialogFooter>
          <Button
            onClick={handleSubmit}
            disabled={submitting}
            size="sm"
          >
            {submitting && <Loader2 className="w-4 h-4 mr-1 animate-spin" />}
            Create Job
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
