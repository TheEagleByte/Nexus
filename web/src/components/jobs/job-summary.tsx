"use client";

import { toast } from "sonner";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { Button } from "@/components/ui/button";
import { Copy, FileText, GitPullRequest } from "lucide-react";

interface JobSummaryProps {
  summary: string;
}

const PR_URL_RE = /github\.com\/.*\/pull\/\d+/;

export function JobSummary({ summary }: JobSummaryProps) {
  async function handleCopy() {
    try {
      await navigator?.clipboard?.writeText(summary);
      toast.success("Summary copied to clipboard");
    } catch {
      toast.error("Failed to copy summary");
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center gap-1.5">
          <FileText className="w-3.5 h-3.5" />
          Summary
        </h2>
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={handleCopy}
          aria-label="Copy summary"
          title="Copy summary"
        >
          <Copy className="w-3.5 h-3.5" />
        </Button>
      </div>
      <div className="rounded-lg border border-border bg-card p-4">
        <ReactMarkdown
          remarkPlugins={[remarkGfm]}
          components={{
            h1: ({ children }) => (
              <h1 className="text-lg font-semibold text-foreground mb-3 mt-4 first:mt-0">
                {children}
              </h1>
            ),
            h2: ({ children }) => (
              <h2 className="text-base font-semibold text-foreground mb-2 mt-3 first:mt-0">
                {children}
              </h2>
            ),
            h3: ({ children }) => (
              <h3 className="text-sm font-semibold text-foreground mb-2 mt-3 first:mt-0">
                {children}
              </h3>
            ),
            p: ({ children }) => (
              <p className="text-sm text-foreground mb-2 last:mb-0">{children}</p>
            ),
            a: ({ href, children }) => {
              const isPR = href && PR_URL_RE.test(href);
              return (
                <a
                  href={href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-primary underline underline-offset-2 hover:text-primary/80 inline-flex items-center gap-1"
                >
                  {isPR && <GitPullRequest className="w-3.5 h-3.5 inline shrink-0" />}
                  {children}
                </a>
              );
            },
            code: ({ children }) => (
              <code className="font-mono text-xs bg-muted px-1 py-0.5 rounded">
                {children}
              </code>
            ),
            pre: ({ children }) => (
              <pre className="mb-2 last:mb-0 [&>code]:block [&>code]:p-3 [&>code]:rounded-md [&>code]:overflow-x-auto [&>code]:px-3 [&>code]:py-3">
                {children}
              </pre>
            ),
            ul: ({ children }) => (
              <ul className="text-sm text-foreground list-disc pl-5 mb-2 space-y-1 last:mb-0">
                {children}
              </ul>
            ),
            ol: ({ children }) => (
              <ol className="text-sm text-foreground list-decimal pl-5 mb-2 space-y-1 last:mb-0">
                {children}
              </ol>
            ),
            li: ({ children }) => <li>{children}</li>,
            table: ({ children }) => (
              <div className="overflow-x-auto mb-2">
                <table className="text-sm border-collapse w-full">{children}</table>
              </div>
            ),
            th: ({ children }) => (
              <th className="border border-border px-3 py-1.5 text-left font-semibold bg-muted text-foreground">
                {children}
              </th>
            ),
            td: ({ children }) => (
              <td className="border border-border px-3 py-1.5 text-foreground">
                {children}
              </td>
            ),
            blockquote: ({ children }) => (
              <blockquote className="border-l-2 border-primary pl-3 text-sm text-muted-foreground mb-2 italic">
                {children}
              </blockquote>
            ),
            hr: () => <hr className="border-border my-3" />,
          }}
        >
          {summary}
        </ReactMarkdown>
      </div>
    </div>
  );
}
