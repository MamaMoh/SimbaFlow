"use client";

import { cn } from "@/lib/utils";
import { appointmentCountdown, TONE_CLASSES } from "@/lib/ui/status";

/**
 * A booked date, with how long there is left to do something about it.
 *
 * The date alone is a lookup: a coordinator scanning forty rows has to work out what today is
 * and subtract, for every row, and so does not. The chip is the part that gets read — amber for
 * this week, red for today or for an appointment that has been and gone with nothing recorded
 * against it.
 *
 * No date at all is the louder case, and keeps its own warning chip: an unbooked appointment is
 * the thing these boards exist to surface.
 */
export function AppointmentCell({
  date,
  pendingLabel,
  stillWaiting = false,
}: {
  date?: string | null;
  pendingLabel: string;
  /** Nothing recorded against the appointment yet, so a date in the past is a missed job. */
  stillWaiting?: boolean;
}) {
  const countdown = appointmentCountdown(date, stillWaiting);

  if (!date || !countdown) {
    return (
      <span
        className={cn(
          "inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium",
          TONE_CLASSES.warning,
        )}
      >
        {pendingLabel}
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-2 whitespace-nowrap">
      <span className="text-sm tabular-nums">{date}</span>
      <span
        className={cn(
          "inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium tabular-nums",
          TONE_CLASSES[countdown.tone],
        )}
      >
        {countdown.label}
      </span>
    </span>
  );
}
