// Beosztás-generáló futás polling. A backend `ScheduleGenerationStatus`
// (Queued|Running|Succeeded|Failed|Cancelled) alapján `intervalMs`-enként
// lekéri a futást, amíg terminál állapot nincs; utána a refetch leáll.
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { services } from "@/services";
import type { ScheduleGenerationRun, ScheduleGenerationStatus } from "@/services/types";

const TERMINAL: ReadonlySet<ScheduleGenerationStatus> = new Set([
  "Succeeded",
  "Failed",
  "Cancelled",
]);

export function isTerminalRunStatus(status: ScheduleGenerationStatus): boolean {
  return TERMINAL.has(status);
}

export function useScheduleRunPolling(runId: string | undefined, intervalMs = 2000) {
  const q = useQuery<ScheduleGenerationRun>({
    enabled: !!runId,
    queryKey: ["scheduleRun", runId],
    queryFn: () => services.scheduleGeneration.get(runId as string),
    placeholderData: keepPreviousData,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (!status) return intervalMs;
      return isTerminalRunStatus(status) ? false : intervalMs;
    },
    refetchIntervalInBackground: false,
  });
  return {
    data: q.data,
    error: q.error,
    isPolling: !!runId && !!q.data && !isTerminalRunStatus(q.data.status),
    isLoading: q.isLoading,
    refetch: q.refetch,
  };
}
