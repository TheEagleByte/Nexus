import { Badge } from "@/components/ui/badge";
import { cn, relativeTime, spokeStatusColor, spokeStatusText } from "@/lib/utils";
import type { SpokeDetailResponse } from "@/types/api";

interface SpokeHeaderProps {
  spoke: SpokeDetailResponse;
}

export function SpokeHeader({ spoke }: SpokeHeaderProps) {
  const profile = spoke.profile;

  return (
    <div className="border-b border-border bg-surface p-4 sm:p-6">
      <div className="flex items-start justify-between mb-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">
            {profile?.displayName ?? spoke.name}
          </h1>
          {profile?.machineDescription && (
            <p className="text-sm text-muted-foreground mt-1">
              {profile.machineDescription}
            </p>
          )}
        </div>
        <div className="flex items-center gap-1.5 shrink-0">
          <div
            className={cn(
              "w-2 h-2 rounded-full",
              spokeStatusColor(spoke.status),
              spoke.status === "online" && "animate-pulse"
            )}
          />
          <span
            className={cn(
              "text-sm font-mono font-semibold capitalize",
              spokeStatusText(spoke.status)
            )}
          >
            {spoke.status}
          </span>
        </div>
      </div>

      <div className="flex flex-wrap gap-4 text-xs text-muted-foreground font-mono mb-4">
        <span>Last sync: {relativeTime(spoke.lastSeen)}</span>
        <span>Jobs completed: {spoke.totalJobsCompleted}</span>
        <span>Active: {spoke.activeJobCount}</span>
      </div>

      {/* Capabilities */}
      <div className="flex flex-wrap gap-1.5 mb-3">
        {spoke.capabilities.map((cap) => (
          <Badge key={cap} variant="secondary" className="font-mono text-xs">
            {cap}
          </Badge>
        ))}
      </div>

      {/* Repos & Integrations */}
      {profile && (
        <div className="flex flex-wrap gap-4 mt-3">
          {profile.repos.length > 0 && (
            <div>
              <span className="text-xs text-muted-foreground block mb-1">
                Repos
              </span>
              <div className="flex flex-wrap gap-1">
                {profile.repos.map((repo) => (
                  <Badge
                    key={repo.name}
                    variant="outline"
                    className="font-mono text-xs"
                  >
                    {repo.name}
                  </Badge>
                ))}
              </div>
            </div>
          )}
          {profile.integrations.length > 0 && (
            <div>
              <span className="text-xs text-muted-foreground block mb-1">
                Integrations
              </span>
              <div className="flex flex-wrap gap-1">
                {profile.integrations.map((integration) => (
                  <Badge
                    key={integration}
                    variant="outline"
                    className="font-mono text-xs capitalize"
                  >
                    {integration}
                  </Badge>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
