"use client";

import { StatusBadge } from "@/components/ui/status-badge";
import { remainingDays, exceptionTone } from "@/lib/ui/status";

/** Days-to-departure chip — delegates to the canonical StatusBadge. */
export function RemainingDaysBadge({ days }: { days: number | null | undefined }) {
  if (days == null) return <span className="text-muted-foreground">—</span>;
  const { tone, label } = remainingDays(days);
  return <StatusBadge tone={tone} label={label} className="tabular-nums" />;
}

/** Exception-case status chip. */
export function ExceptionStatusBadge({ status }: { status: string }) {
  return <StatusBadge tone={exceptionTone(status)} value={status} />;
}
