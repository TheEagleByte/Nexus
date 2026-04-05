import { fetchSpokes } from "@/lib/api";
import { SpokeGrid } from "@/components/dashboard/spoke-grid";
import type { SpokeResponse } from "@/types/api";

export default async function DashboardPage() {
  let initialSpokes: SpokeResponse[] = [];
  try {
    const data = await fetchSpokes();
    initialSpokes = data.spokes;
  } catch {
    // API not reachable — start with empty state
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Dashboard</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Spoke overview and real-time status
        </p>
      </div>
      <SpokeGrid initialSpokes={initialSpokes} />
    </div>
  );
}
