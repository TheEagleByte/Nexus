"use client";

import { useEffect, useRef, useState, useCallback, type MutableRefObject } from "react";
import Ansi from "ansi-to-react";
import { useSignalR } from "@/lib/signalr";
import { stripAnsi } from "@/lib/utils";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { ArrowDown, Search } from "lucide-react";
import type { OutputChunk, JobOutputReceivedEvent } from "@/types/api";

const MAX_CHUNKS = 5000;

interface TerminalOutputProps {
  jobId: string;
  initialChunks: OutputChunk[];
  isComplete: boolean;
  onContentRef?: MutableRefObject<() => string>;
}

export function TerminalOutput({
  jobId,
  initialChunks,
  isComplete: initialIsComplete,
  onContentRef,
}: TerminalOutputProps) {
  const [chunks, setChunks] = useState<OutputChunk[]>(initialChunks);
  const [autoScroll, setAutoScroll] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");
  const [showSearch, setShowSearch] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const { subscribeJobOutput, unsubscribeJobOutput, jobUpdates } = useSignalR();

  // Derive completion from props + SignalR (no effect needed)
  const jobUpdate = jobUpdates.get(jobId);
  const isComplete =
    initialIsComplete ||
    jobUpdate?.newStatus === "completed" ||
    jobUpdate?.newStatus === "failed" ||
    jobUpdate?.newStatus === "cancelled";

  // Subscribe to real-time output
  useEffect(() => {
    const handleChunk = (event: JobOutputReceivedEvent) => {
      const chunk: OutputChunk = {
        sequence: event.sequence,
        content: event.content,
        streamType: event.streamType,
        timestamp: event.timestamp,
      };
      setChunks((prev) => {
        const next = [...prev, chunk];
        if (next.length > MAX_CHUNKS) {
          return next.slice(next.length - MAX_CHUNKS);
        }
        return next;
      });
    };

    subscribeJobOutput(jobId, handleChunk);
    return () => unsubscribeJobOutput(jobId, handleChunk);
  }, [jobId, subscribeJobOutput, unsubscribeJobOutput]);

  // Keep content ref in sync for external copy
  useEffect(() => {
    if (onContentRef) {
      onContentRef.current = () => chunks.map((c) => stripAnsi(c.content)).join("");
    }
  }, [chunks, onContentRef]);

  // Auto-scroll
  useEffect(() => {
    if (autoScroll && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [chunks, autoScroll]);

  // Detect manual scroll
  const handleScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 50;
    setAutoScroll(atBottom);
  }, []);

  const jumpToBottom = () => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
      setAutoScroll(true);
    }
  };

  const filteredChunks = searchQuery
    ? chunks.filter((c) =>
        stripAnsi(c.content).toLowerCase().includes(searchQuery.toLowerCase())
      )
    : chunks;

  const truncated = chunks.length >= MAX_CHUNKS;

  return (
    <div className="relative flex flex-col rounded-md border border-border bg-background overflow-hidden">
      {/* Terminal header */}
      <div className="flex items-center justify-between px-3 py-2 border-b border-border bg-surface text-xs">
        <div className="flex items-center gap-2">
          <div className="flex gap-1.5">
            <div className="w-2.5 h-2.5 rounded-full bg-status-error" />
            <div className="w-2.5 h-2.5 rounded-full bg-status-warning" />
            <div className="w-2.5 h-2.5 rounded-full bg-status-success" />
          </div>
          <span className="font-mono text-muted-foreground">
            output &mdash; {chunks.length} chunks
          </span>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={() => setShowSearch(!showSearch)}
          >
            <Search className="w-3.5 h-3.5" />
          </Button>
          {isComplete ? (
            <span className="text-status-success font-mono">done</span>
          ) : (
            <span className="text-status-warning font-mono animate-pulse">
              streaming
            </span>
          )}
        </div>
      </div>

      {/* Search bar */}
      {showSearch && (
        <div className="px-3 py-2 border-b border-border bg-surface">
          <Input
            placeholder="Search output..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="h-7 text-xs font-mono"
            autoFocus
          />
        </div>
      )}

      {/* Output area */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="relative flex-1 overflow-y-auto p-3 min-h-[200px] max-h-[600px] font-mono text-xs leading-5"
      >
        {truncated && (
          <div className="text-center text-muted-foreground mb-2 py-1 border-b border-border">
            Earlier output truncated ({MAX_CHUNKS} chunk limit)
          </div>
        )}
        {filteredChunks.map((chunk) => (
          <div
            key={chunk.sequence}
            className={
              chunk.streamType === "stderr"
                ? "border-l-2 border-status-error pl-2 whitespace-pre-wrap break-all"
                : "text-foreground whitespace-pre-wrap break-all"
            }
          >
            {searchQuery ? (
              <HighlightedText
                text={stripAnsi(chunk.content)}
                query={searchQuery}
              />
            ) : (
              <Ansi>{chunk.content}</Ansi>
            )}
          </div>
        ))}
        {!isComplete && (
          <span className="text-primary animate-pulse">&#9608;</span>
        )}
        {filteredChunks.length === 0 && chunks.length > 0 && searchQuery && (
          <div className="text-muted-foreground text-center py-4">
            No matches for &ldquo;{searchQuery}&rdquo;
          </div>
        )}
        {chunks.length === 0 && (
          <div className="text-muted-foreground text-center py-4">
            {isComplete ? "No output recorded" : "Waiting for output..."}
          </div>
        )}
      </div>

      {/* Jump to bottom */}
      {!autoScroll && (
        <div className="absolute bottom-14 right-6">
          <Button
            size="sm"
            variant="secondary"
            onClick={jumpToBottom}
            className="shadow-lg"
          >
            <ArrowDown className="w-3.5 h-3.5 mr-1" />
            Bottom
          </Button>
        </div>
      )}
    </div>
  );
}

function HighlightedText({ text, query }: { text: string; query: string }) {
  if (!query) return <>{text}</>;

  const parts: { text: string; match: boolean }[] = [];
  const lower = text.toLowerCase();
  const lowerQuery = query.toLowerCase();
  let lastIndex = 0;

  let idx = lower.indexOf(lowerQuery);
  while (idx !== -1) {
    if (idx > lastIndex) {
      parts.push({ text: text.slice(lastIndex, idx), match: false });
    }
    parts.push({
      text: text.slice(idx, idx + query.length),
      match: true,
    });
    lastIndex = idx + query.length;
    idx = lower.indexOf(lowerQuery, lastIndex);
  }
  if (lastIndex < text.length) {
    parts.push({ text: text.slice(lastIndex), match: false });
  }

  return (
    <>
      {parts.map((part, i) =>
        part.match ? (
          <mark key={i} className="bg-primary/30 text-foreground rounded-sm px-0.5">
            {part.text}
          </mark>
        ) : (
          <span key={i}>{part.text}</span>
        )
      )}
    </>
  );
}
