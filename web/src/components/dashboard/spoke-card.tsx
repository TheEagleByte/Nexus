"use client";

import Link from "next/link";
import { cn, relativeTime, spokeStatusColor, spokeStatusText } from "@/lib/utils";
import type { SpokeResponse } from "@/types/api";

interface SpokeCardProps {
  spoke: SpokeResponse;
}

export function SpokeCard({ spoke }: SpokeCardProps) {
  return (
    <Link
      href={`/spokes/${spoke.id}`}
      className="group block rounded-md border border-border bg-card p-4 transition-colors duration-150 hover:border-primary"
    >
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-foreground truncate">
          {spoke.name}
        </h3>
        <div className="flex items-center gap-1.5 shrink-0">
          <div
            className={cn(
              "w-1.5 h-1.5 rounded-full",
              spokeStatusColor(spoke.status),
              spoke.status === "online" && "animate-pulse"
            )}
          />
          <span
            className={cn(
              "text-xs font-mono font-semibold capitalize",
              spokeStatusText(spoke.status)
            )}
          >
            {spoke.status}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 mb-3">
        <div className="bg-surface-accent rounded px-2.5 py-1.5">
          <div className="text-xs text-muted-foreground">Jobs</div>
          <div className="text-sm font-mono font-semibold text-foreground">
            {spoke.activeJobCount}
          </div>
        </div>
        <div className="bg-surface-accent rounded px-2.5 py-1.5">
          <div className="text-xs text-muted-foreground">Capabilities</div>
          <div className="text-sm font-mono font-semibold text-foreground">
            {spoke.capabilities.length}
          </div>
        </div>
      </div>

      <div className="text-xs text-muted-foreground font-mono">
        Last seen {relativeTime(spoke.lastSeen)}
      </div>
    </Link>
  );
}
