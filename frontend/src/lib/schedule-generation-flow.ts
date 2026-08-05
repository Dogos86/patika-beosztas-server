import type { QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/services/http/errors";
import type { AdminScheduleService } from "@/services/interfaces";
import type { RegenerationScopeInput, ScheduleGenerationRun } from "@/services/types";

export async function refreshScheduleAfterGeneration(
  queryClient: QueryClient,
  scheduleId: string,
): Promise<void> {
  await queryClient.invalidateQueries({ queryKey: ["schedules"] });
  await queryClient.refetchQueries({ queryKey: ["schedule", scheduleId, "detail"] });
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ["schedule", scheduleId, "matrix"] }),
    queryClient.invalidateQueries({ queryKey: ["schedule", scheduleId, "coverage"] }),
    queryClient.invalidateQueries({ queryKey: ["schedule", scheduleId, "issues"] }),
    queryClient.invalidateQueries({ queryKey: ["schedule", scheduleId, "changes"] }),
  ]);
}

export async function regenerateWithLatestScheduleVersion(
  queryClient: QueryClient,
  scheduleService: Pick<AdminScheduleService, "get" | "regenerate">,
  scheduleId: string,
  scope: RegenerationScopeInput,
): Promise<ScheduleGenerationRun> {
  const latest = await queryClient.fetchQuery({
    queryKey: ["schedule", scheduleId, "detail"],
    queryFn: () => scheduleService.get(scheduleId),
    staleTime: 0,
  });
  return scheduleService.regenerate(scheduleId, {
    scope,
    expectedVersion: latest.version,
  });
}

export function isConcurrencyError(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}

export const SCHEDULE_REFRESHED_MESSAGE =
  "A beosztás közben frissült. Az adatokat újratöltöttük, indítsd újra a műveletet.";
