"use client";

import type { ReactNode } from "react";
import { SignalRProvider } from "@/lib/signalr";
import { Header } from "@/components/layout/header";
import { Sidebar } from "@/components/layout/sidebar";
import { MobileNav } from "@/components/layout/mobile-nav";
import { ConnectionBanner } from "@/components/layout/connection-banner";
import { Toaster } from "@/components/ui/sonner";
import type { SpokeResponse, ConversationSummary } from "@/types/api";

interface AppShellProps {
  initialSpokes: SpokeResponse[];
  initialConversations: ConversationSummary[];
  signalrUrl: string;
  children: ReactNode;
}

export function AppShell({ initialSpokes, initialConversations, signalrUrl, children }: AppShellProps) {
  return (
    <SignalRProvider hubUrl={signalrUrl}>
      <Header />
      <ConnectionBanner />
      <div className="flex flex-1">
        <Sidebar initialSpokes={initialSpokes} initialConversations={initialConversations} />
        <main className="flex-1 p-4 sm:p-6 pb-20 lg:pb-6">
          {children}
        </main>
      </div>
      <MobileNav />
      <Toaster position="bottom-right" />
    </SignalRProvider>
  );
}
