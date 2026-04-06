import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { AppShell } from "./app-shell";
import { fetchSpokes, fetchConversations } from "@/lib/api";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Nexus — Hub Dashboard",
  description: "Nexus Hub UI — Spoke management and monitoring dashboard",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  let initialSpokes: Awaited<ReturnType<typeof fetchSpokes>>["spokes"] = [];
  let initialConversations: Awaited<ReturnType<typeof fetchConversations>>["conversations"] = [];
  try {
    const [spokesData, conversationsData] = await Promise.all([
      fetchSpokes(),
      fetchConversations(),
    ]);
    initialSpokes = spokesData.spokes;
    initialConversations = conversationsData.conversations;
  } catch {
    // API not reachable — start with empty state
  }

  const signalrUrl =
    process.env.NEXT_PUBLIC_HUB_SIGNALR_URL ?? "http://localhost:5000/hubs/nexus";

  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} dark h-full antialiased`}
    >
      <body className="min-h-full flex flex-col bg-background text-foreground font-sans">
        <AppShell initialSpokes={initialSpokes} initialConversations={initialConversations} signalrUrl={signalrUrl}>
          {children}
        </AppShell>
      </body>
    </html>
  );
}
