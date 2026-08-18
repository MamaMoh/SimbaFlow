"use client";

import { cn } from "@/lib/utils";
import { ageTone, TONE_CLASSES } from "@/lib/ui/status";

/**
 * Case age as a colour-coded chip.
 *
 * Replaces a bare number: on a board of 200 rows the colour is what a supervisor actually reads,
 * and the number only matters once the colour has drawn the eye.
 */
export function AgeCell({
  days,
  suffix = "d",
  title,
}: {
  days?: number | null;
  suffix?: string;
  title?: string;
}) {
  if (days == null) return <span className="text-muted-foreground">—</span>;

  return (
    <span
      title={title ?? `${days} day${days === 1 ? "" : "s"}`}
      className={cn(
        "inline-flex min-w-[3rem] items-center justify-center rounded-full border px-2 py-0.5 text-xs font-medium tabular-nums",
        TONE_CLASSES[ageTone(days)]
      )}
    >
      {days}
      {suffix}
    </span>
  );
}
