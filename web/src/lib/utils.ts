import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";
import type { SpokeStatus, ProjectStatus, JobStatus } from "@/types/api";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function relativeTime(iso: string): string {
  const now = Date.now();
  const then = new Date(iso).getTime();
  const diff = now - then;

  if (diff < 0) return "just now";

  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return "just now";

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;

  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export function spokeStatusColor(status: SpokeStatus): string {
  switch (status) {
    case "online":
      return "bg-status-success";
    case "offline":
      return "bg-status-error";
    case "busy":
      return "bg-status-warning";
  }
}

export function spokeStatusText(status: SpokeStatus): string {
  switch (status) {
    case "online":
      return "text-status-success";
    case "offline":
      return "text-status-error";
    case "busy":
      return "text-status-warning";
  }
}

export function projectStatusColor(status: ProjectStatus): string {
  switch (status) {
    case "active":
      return "bg-status-success/20 text-status-success";
    case "planning":
      return "bg-status-info/20 text-status-info";
    case "paused":
      return "bg-status-warning/20 text-status-warning";
    case "completed":
      return "bg-status-success/20 text-status-success";
    case "failed":
      return "bg-status-error/20 text-status-error";
    case "archived":
      return "bg-muted text-muted-foreground";
  }
}

export function jobStatusColor(status: JobStatus): string {
  switch (status) {
    case "queued":
      return "bg-status-info/20 text-status-info";
    case "awaiting_approval":
      return "bg-[#00d9ff]/20 text-[#00d9ff]";
    case "running":
      return "bg-status-warning/20 text-status-warning";
    case "completed":
      return "bg-status-success/20 text-status-success";
    case "failed":
      return "bg-status-error/20 text-status-error";
    case "cancelled":
      return "bg-muted text-muted-foreground";
  }
}
