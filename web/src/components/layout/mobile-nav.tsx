"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
  LayoutDashboard,
  Monitor,
  FolderKanban,
  Briefcase,
  Settings,
} from "lucide-react";

const navItems = [
  { path: "/", label: "Dashboard", Icon: LayoutDashboard },
  { path: "/spokes", label: "Spokes", Icon: Monitor },
  { path: "/projects", label: "Projects", Icon: FolderKanban },
  { path: "/jobs", label: "Jobs", Icon: Briefcase },
  { path: "/settings", label: "Settings", Icon: Settings },
];

export function MobileNav() {
  const pathname = usePathname();

  return (
    <nav className="fixed bottom-0 left-0 right-0 h-14 border-t border-border bg-surface lg:hidden z-50">
      <div className="flex h-full items-center justify-around">
        {navItems.map((item) => {
          const isActive =
            item.path === "/"
              ? pathname === "/"
              : pathname.startsWith(item.path);
          return (
            <Link
              key={item.path}
              href={item.path}
              className={cn(
                "flex flex-col items-center justify-center w-full h-full text-xs font-medium",
                isActive ? "text-primary" : "text-muted-foreground"
              )}
            >
              <item.Icon className="w-5 h-5 mb-1" />
              <span className="truncate">{item.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
