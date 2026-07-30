import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { services } from "@/services";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/common/states";
import { OpeningHoursEditor } from "./OpeningHoursEditor";
import { defaultOpeningHours, validateOpeningHours } from "@/lib/opening-hours";
import type { LocationOpeningHours } from "@/services/types";

interface Props {
  locationId: string;
  canEdit: boolean;
}

/** A heti nyitvatartás önálló erőforrás: saját query, mutation és verzió. */
export function LocationOpeningHoursTab({ locationId, canEdit }: Props) {
  const qc = useQueryClient();
  const openingQ = useQuery({
    queryKey: ["location-opening", locationId],
    queryFn: () => services.location.getWeeklyOpening(locationId),
  });
  const [draft, setDraft] = useState<LocationOpeningHours | null>(null);

  useEffect(() => {
    if (openingQ.data) setDraft(openingQ.data.hours);
    else if (openingQ.isSuccess) setDraft(defaultOpeningHours());
  }, [openingQ.data, openingQ.isSuccess]);

  const saveMut = useMutation({
    mutationFn: (hours: LocationOpeningHours) =>
      services.location.updateWeeklyOpening(locationId, hours, openingQ.data?.version ?? null),
    onSuccess: () => {
      toast.success("Nyitvatartás mentve.");
      void qc.invalidateQueries({ queryKey: ["location-opening", locationId] });
    },
    onError: (e) =>
      toast.error("A nyitvatartás mentése nem sikerült.", { description: (e as Error).message }),
  });

  if (openingQ.isLoading) return <LoadingState />;
  if (openingQ.isError) {
    return (
      <p className="text-sm text-destructive">
        A nyitvatartás betöltése nem sikerült: {(openingQ.error as Error).message}
      </p>
    );
  }
  if (!draft) return <LoadingState />;

  return (
    <div className="space-y-3">
      {(openingQ.data?.warnings ?? []).map((w) => (
        <p key={w} className="text-xs text-muted-foreground">
          {w}
        </p>
      ))}
      <OpeningHoursEditor value={draft} onChange={setDraft} />
      <div className="flex justify-end">
        <Button
          type="button"
          disabled={!canEdit || saveMut.isPending}
          onClick={() => {
            const errs = validateOpeningHours(draft);
            if (errs.length > 0) {
              toast.error("A nyitvatartás nem érvényes.", { description: errs[0].message });
              return;
            }
            saveMut.mutate(draft);
          }}
        >
          Nyitvatartás mentése
        </Button>
      </div>
    </div>
  );
}
