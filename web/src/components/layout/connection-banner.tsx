"use client";

import { useSignalR } from "@/lib/signalr";

export function ConnectionBanner() {
  const { connectionState } = useSignalR();

  if (connectionState === "Connected") return null;

  const message =
    connectionState === "Reconnecting"
      ? "Connection lost — reconnecting..."
      : "Disconnected from hub";

  return (
    <div className="bg-status-error/10 border-b border-status-error/30 px-4 py-2 text-center text-sm font-mono text-status-error">
      {message}
    </div>
  );
}
