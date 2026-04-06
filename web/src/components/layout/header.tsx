"use client";

import Link from "next/link";
import { useSignalR } from "@/lib/signalr";

export function Header() {
  const { connectionState } = useSignalR();
  const isConnected = connectionState === "Connected";

  return (
    <header className="h-14 border-b border-border bg-surface sticky top-0 z-50">
      <div className="h-full px-4 sm:px-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/" className="text-lg font-bold text-primary">
            Nexus
          </Link>
          <span className="text-xs text-muted-foreground font-mono hidden sm:inline">
            /
          </span>
          <span className="text-sm text-muted-foreground hidden sm:inline">
            Dashboard
          </span>
        </div>
        <div className="flex items-center gap-2">
          {isConnected ? (
            <div className="flex items-center gap-1.5">
              <div className="h-2 w-2 rounded-full bg-primary animate-pulse" />
              <span className="text-xs font-mono text-primary">LIVE</span>
            </div>
          ) : (
            <div className="flex items-center gap-1.5">
              <div className="h-2 w-2 rounded-full bg-status-error" />
              <span className="text-xs font-mono text-status-error">
                {connectionState === "Reconnecting"
                  ? "RECONNECTING"
                  : "OFFLINE"}
              </span>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
