"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { Send, ArrowDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { useSignalR } from "@/lib/signalr";
import { sendConversationMessage } from "@/lib/api-client";
import type {
  ConversationDetail,
  ConversationMessage,
  ConversationMessageReceivedEvent,
} from "@/types/api";

interface ConversationViewProps {
  initialConversation: ConversationDetail;
}

export function ConversationView({ initialConversation }: ConversationViewProps) {
  const { subscribeConversationMessages, unsubscribeConversationMessages } =
    useSignalR();
  const [messages, setMessages] = useState<ConversationMessage[]>(
    initialConversation.messages
  );
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [showScrollButton, setShowScrollButton] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const isAtBottomRef = useRef(true);

  const scrollToBottom = useCallback(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, []);

  // Auto-scroll when new messages arrive and user is at bottom
  useEffect(() => {
    if (isAtBottomRef.current) {
      scrollToBottom();
    }
  }, [messages, scrollToBottom]);

  // Subscribe to real-time conversation messages
  useEffect(() => {
    const handleMessage = (event: ConversationMessageReceivedEvent) => {
      setMessages((prev) => {
        // Avoid duplicates (optimistic + server echo)
        if (prev.some((m) => m.id === event.messageId)) return prev;
        return [
          ...prev,
          {
            id: event.messageId,
            conversationId: event.conversationId,
            role: event.role,
            content: event.content,
            timestamp: event.timestamp,
          },
        ];
      });
    };

    subscribeConversationMessages(initialConversation.id, handleMessage);
    return () =>
      unsubscribeConversationMessages(initialConversation.id, handleMessage);
  }, [
    initialConversation.id,
    subscribeConversationMessages,
    unsubscribeConversationMessages,
  ]);

  function handleScroll() {
    if (!scrollRef.current) return;
    const { scrollTop, scrollHeight, clientHeight } = scrollRef.current;
    const atBottom = scrollHeight - scrollTop - clientHeight < 50;
    isAtBottomRef.current = atBottom;
    setShowScrollButton(!atBottom);
  }

  async function handleSend() {
    const content = input.trim();
    if (!content || sending) return;

    // Optimistic add
    const optimisticId = crypto.randomUUID();
    const optimisticMsg: ConversationMessage = {
      id: optimisticId,
      conversationId: initialConversation.id,
      role: "user",
      content,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, optimisticMsg]);
    setInput("");
    setSending(true);

    try {
      const serverMsg = await sendConversationMessage(initialConversation.id, {
        content,
      });
      // Replace optimistic with server message
      setMessages((prev) =>
        prev.map((m) => (m.id === optimisticId ? serverMsg : m))
      );
    } catch (err) {
      console.error("Failed to send message:", err);
      // Remove optimistic message on failure
      setMessages((prev) => prev.filter((m) => m.id !== optimisticId));
      setInput(content); // restore input
    } finally {
      setSending(false);
    }
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  }

  return (
    <div className="flex flex-col h-[calc(100vh-3.5rem-3rem)]">
      {/* Header */}
      <div className="flex items-center gap-3 px-4 py-3 border-b border-border shrink-0">
        <h1 className="text-lg font-semibold truncate">
          {initialConversation.title}
        </h1>
        {initialConversation.spokeName && (
          <span className="text-xs text-muted-foreground bg-surface-accent px-2 py-0.5 rounded-sm">
            {initialConversation.spokeName}
          </span>
        )}
      </div>

      {/* Messages */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto px-4 py-4 space-y-4"
      >
        {messages.length === 0 && (
          <p className="text-center text-muted-foreground py-8">
            No messages yet. Send a message to start the conversation.
          </p>
        )}
        {messages.map((msg) => (
          <div
            key={msg.id}
            className={cn(
              "flex",
              msg.role === "user" ? "justify-end" : "justify-start"
            )}
          >
            <div
              className={cn(
                "max-w-[75%] rounded-lg px-4 py-2.5",
                msg.role === "user"
                  ? "bg-primary text-primary-foreground"
                  : "bg-surface-accent text-foreground"
              )}
            >
              <div className="text-sm whitespace-pre-wrap break-words">
                {msg.content}
              </div>
              <div
                className={cn(
                  "text-xs mt-1",
                  msg.role === "user"
                    ? "text-primary-foreground/60"
                    : "text-muted-foreground"
                )}
              >
                {new Date(msg.timestamp).toLocaleTimeString()}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Scroll to bottom button */}
      {showScrollButton && (
        <div className="absolute bottom-24 right-8">
          <button
            onClick={scrollToBottom}
            className="bg-surface-accent hover:bg-muted p-2 rounded-full shadow-md transition-colors"
          >
            <ArrowDown className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Input */}
      <div className="border-t border-border px-4 py-3 shrink-0">
        <div className="flex gap-2 items-end">
          <textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type a message..."
            rows={1}
            className="flex-1 resize-none rounded-sm border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring min-h-[2.5rem] max-h-32"
            disabled={sending}
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || sending}
            className={cn(
              "p-2.5 rounded-sm transition-colors shrink-0",
              input.trim() && !sending
                ? "bg-primary text-primary-foreground hover:bg-primary/90"
                : "bg-muted text-muted-foreground cursor-not-allowed"
            )}
          >
            <Send className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
