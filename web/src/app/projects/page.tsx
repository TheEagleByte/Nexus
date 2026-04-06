import Link from "next/link";
import { fetchAllProjects } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { projectStatusColor, relativeTime } from "@/lib/utils";
import type { ProjectResponse } from "@/types/api";

export default async function ProjectsPage() {
  let projects: ProjectResponse[] = [];
  let total = 0;
  try {
    const data = await fetchAllProjects({ limit: 100 });
    projects = data.projects;
    total = data.total;
  } catch {
    // API unavailable
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Projects</h1>
        <span className="text-sm font-mono text-muted-foreground">
          {total} total
        </span>
      </div>

      {projects.length > 0 ? (
        <div className="space-y-2">
          {projects.map((project) => (
            <Link
              key={project.id}
              href={`/projects/${project.id}`}
              className="flex items-center gap-4 rounded border border-border bg-card p-4 transition-colors hover:border-primary/50"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-sm font-medium text-foreground">
                    {project.name}
                  </span>
                  {project.externalKey && (
                    <span className="text-xs font-mono text-muted-foreground">
                      {project.externalKey}
                    </span>
                  )}
                </div>
                {project.summary && (
                  <p className="text-xs text-muted-foreground line-clamp-1 mb-1">
                    {project.summary}
                  </p>
                )}
                <div className="flex gap-3 text-xs text-muted-foreground font-mono">
                  <span>{project.spokeName || "—"}</span>
                  <span>Updated {relativeTime(project.updatedAt)}</span>
                </div>
              </div>
              <Badge
                variant="secondary"
                className={`text-xs font-mono shrink-0 ${projectStatusColor(project.status)}`}
              >
                {project.status}
              </Badge>
            </Link>
          ))}
        </div>
      ) : (
        <div className="rounded-md border border-border bg-card p-8 text-center">
          <p className="text-sm text-muted-foreground">No projects yet.</p>
        </div>
      )}
    </div>
  );
}
