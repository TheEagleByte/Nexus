import { Badge } from "@/components/ui/badge";
import { projectStatusColor, relativeTime } from "@/lib/utils";
import type { ProjectResponse } from "@/types/api";

interface ProjectsListProps {
  projects: ProjectResponse[];
}

export function ProjectsList({ projects }: ProjectsListProps) {
  if (projects.length === 0) {
    return (
      <div className="p-4 text-sm text-muted-foreground">
        No projects yet
      </div>
    );
  }

  return (
    <div className="space-y-2 p-4">
      {projects.map((project) => (
        <div
          key={project.id}
          className="rounded border border-border bg-background p-3"
        >
          <div className="flex items-center justify-between mb-1">
            <span className="text-sm font-medium text-foreground truncate">
              {project.name}
            </span>
            <Badge
              variant="secondary"
              className={`text-xs font-mono ${projectStatusColor(project.status)}`}
            >
              {project.status}
            </Badge>
          </div>
          {project.summary && (
            <p className="text-xs text-muted-foreground mb-1.5 line-clamp-2">
              {project.summary}
            </p>
          )}
          <div className="flex gap-3 text-xs text-muted-foreground font-mono">
            <span>
              {project.activeJobCount} active / {project.totalJobCount} total
            </span>
            <span>Updated {relativeTime(project.updatedAt)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
