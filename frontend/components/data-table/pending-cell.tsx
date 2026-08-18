"use client";

import { cn } from "@/lib/utils";
import { TONE_CLASSES } from "@/lib/ui/status";

/**
 * A value that is supposed to exist but often doesn't yet.
 *
 * Rendering an empty cell hides the work; the whole point of these boards is that a missing
 * appointment or unbooked ticket is the thing a coordinator needs to spot and act on.
 */
export function PendingCell({
  value,
  pendingLabel,
}: {
  value?: string | null;
  pendingLabel: string;
}) {
  if (value) return <span className="text-sm tabular-nums">{value}</span>;

  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium",
        TONE_CLASSES.warning
      )}
    >
      {pendingLabel}
    </span>
  );
}
