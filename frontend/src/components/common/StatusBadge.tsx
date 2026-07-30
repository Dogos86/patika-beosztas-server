import { Badge } from "@/components/ui/badge";
import { leaveStatusLabel } from "@/lib/format";
import type { LeaveStatus } from "@/services/types";

const styles: Record<LeaveStatus, string> = {
  draft: "bg-slate-100 text-slate-700 border-slate-200",
  pending: "bg-amber-100 text-amber-800 border-amber-200",
  approved: "bg-emerald-100 text-emerald-800 border-emerald-200",
  rejected: "bg-rose-100 text-rose-800 border-rose-200",
  withdrawn: "bg-slate-100 text-slate-700 border-slate-200",
  cancelled: "bg-slate-200 text-slate-700 border-slate-300",
  reported: "bg-sky-100 text-sky-800 border-sky-200",
  recorded: "bg-sky-100 text-sky-800 border-sky-200",
  closed: "bg-slate-100 text-slate-600 border-slate-200",
};

export function StatusBadge({ status }: { status: LeaveStatus }) {
  return (
    <Badge variant="outline" className={styles[status]}>
      {leaveStatusLabel(status)}
    </Badge>
  );
}
