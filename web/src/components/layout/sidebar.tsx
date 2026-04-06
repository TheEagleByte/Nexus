"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Layers, FolderOpen } from "lucide-react";
import { cn, spokeStatusColor } from "@/lib/utils";
import { useSignalR } from "@/lib/signalr";
import type { SpokeResponse } from "@/types/api";

interface SidebarProps {
  initialSpokes: SpokeResponse[];
}

export function Sidebar({ initialSpokes }: SidebarProps) {
  const pathname = usePathname();
  const { spokes: realtimeSpokes } = useSignalR();

  // Merge real-time updates over initial data
  const spokes = initialSpokes.map(
    (s) => realtimeSpokes.get(s.id) ?? s
  );

  // Also add any spokes that appeared via SignalR but weren't in the initial load
  for (const [id, spoke] of realtimeSpokes) {
    if (!initialSpokes.some((s) => s.id === id)) {
      spokes.push(spoke);
    }
  }

  return (
    <aside className="hidden lg:flex lg:flex-col lg:w-60 border-r border-border bg-surface h-[calc(100vh-3.5rem)] sticky top-14 overflow-y-auto">
      {/* Navigation links */}
      <div className="p-4 pb-2 space-y-1">
        <Link
          href="/jobs"
          className={cn(
            "flex items-center gap-2 px-3 py-2 rounded-sm text-sm font-medium transition-colors duration-150",
            pathname.startsWith("/jobs")
              ? "bg-primary text-primary-foreground"
              : "text-foreground hover:bg-surface-accent"
          )}
        >
          <Layers className="w-4 h-4" />
          Jobs
        </Link>
        <Link
          href="/projects"
          className={cn(
            "flex items-center gap-2 px-3 py-2 rounded-sm text-sm font-medium transition-colors duration-150",
            pathname.startsWith("/projects")
              ? "bg-primary text-primary-foreground"
              : "text-foreground hover:bg-surface-accent"
          )}
        >
          <FolderOpen className="w-4 h-4" />
          Projects
        </Link>
      </div>

      <div className="border-b border-border mx-4" />

      {/* Spokes list */}
      <div className="p-4">
        <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-3">
          Spokes
        </h2>
        <nav className="space-y-1">
          {spokes.map((spoke) => {
            const isActive = pathname === `/spokes/${spoke.id}`;
            return (
              <Link
                key={spoke.id}
                href={`/spokes/${spoke.id}`}
                className={cn(
                  "block w-full text-left px-3 py-2.5 rounded-sm text-sm font-medium transition-colors duration-150",
                  isActive
                    ? "bg-primary text-primary-foreground"
                    : "text-foreground hover:bg-surface-accent"
                )}
              >
                <div className="flex items-center gap-2">
                  <div
                    className={cn(
                      "w-2 h-2 rounded-full shrink-0",
                      spokeStatusColor(spoke.status),
                      spoke.status === "online" && "animate-pulse"
                    )}
                  />
                  <span className="truncate">{spoke.name}</span>
                </div>
                <div className="text-xs text-muted-foreground mt-0.5 pl-4">
                  {spoke.activeJobCount} job
                  {spoke.activeJobCount !== 1 ? "s" : ""}
                </div>
              </Link>
            );
          })}
          {spokes.length === 0 && (
            <p className="text-xs text-muted-foreground px-3 py-2">
              No spokes registered
            </p>
          )}
        </nav>
      </div>
    </aside>
  );
}
