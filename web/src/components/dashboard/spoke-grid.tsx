"use client";

import { useSignalR } from "@/lib/signalr";
import { SpokeCard } from "./spoke-card";
import type { SpokeResponse } from "@/types/api";

interface SpokeGridProps {
  initialSpokes: SpokeResponse[];
}

export function SpokeGrid({ initialSpokes }: SpokeGridProps) {
  const { spokes: realtimeSpokes } = useSignalR();

  // Merge real-time updates over initial server data
  const spokes = initialSpokes.map(
    (s) => realtimeSpokes.get(s.id) ?? s
  );

  // Add any new spokes that arrived via SignalR
  for (const [id, spoke] of realtimeSpokes) {
    if (!initialSpokes.some((s) => s.id === id)) {
      spokes.push(spoke);
    }
  }

  if (spokes.length === 0) {
    return (
      <div className="flex items-center justify-center py-16">
        <div className="text-center">
          <div className="text-4xl mb-4">{">"}_</div>
          <h2 className="text-lg font-semibold text-foreground mb-2">
            No spokes registered
          </h2>
          <p className="text-sm text-muted-foreground max-w-md">
            Start a spoke daemon to register it with the hub. Spokes will appear
            here once connected.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {spokes.map((spoke) => (
        <SpokeCard key={spoke.id} spoke={spoke} />
      ))}
    </div>
  );
}
