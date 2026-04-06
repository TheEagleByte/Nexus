"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogClose,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { createConversation } from "@/lib/api-client";
import type { SpokeResponse } from "@/types/api";

interface NewThreadDialogProps {
  spokes: SpokeResponse[];
}

export function NewThreadDialog({ spokes }: NewThreadDialogProps) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [spokeId, setSpokeId] = useState<string>("");
  const [loading, setLoading] = useState(false);

  async function handleCreate() {
    if (!title.trim()) return;
    setLoading(true);
    try {
      const conv = await createConversation({
        title: title.trim(),
        spokeId: spokeId || null,
      });
      setOpen(false);
      setTitle("");
      setSpokeId("");
      router.push(`/conversations/${conv.id}`);
    } catch (err) {
      console.error("Failed to create conversation:", err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <button
          className="p-1 rounded-sm text-muted-foreground hover:text-foreground hover:bg-surface-accent transition-colors"
          aria-label="New conversation"
        >
          <Plus className="w-4 h-4" />
        </button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New Conversation</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label htmlFor="title">Title</Label>
            <Input
              id="title"
              placeholder="What do you want to work on?"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !loading) handleCreate();
              }}
              autoFocus
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="spoke">Spoke (optional)</Label>
            <select
              id="spoke"
              value={spokeId}
              onChange={(e) => setSpokeId(e.target.value)}
              className="flex h-9 w-full rounded-sm border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Hub-level (no spoke)</option>
              {spokes.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.status})
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <DialogClose asChild>
            <Button variant="outline">Cancel</Button>
          </DialogClose>
          <Button onClick={handleCreate} disabled={!title.trim() || loading}>
            {loading ? "Creating..." : "Create"}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
