"use client";

import { cn } from "@/lib/utils";
import {
  type StatusTone,
  TONE_CLASSES,
  TONE_DOT,
  statusTone,
} from "@/lib/ui/status";

export type StatusBadgeProps = {
  /** Explicit tone. If omitted, derived from `value` via statusTone(). */
  tone?: StatusTone;
  /** Raw status value; used as the label and (when tone is omitted) to derive the tone. */
  value?: string | null;
  /** Override the displayed text (defaults to `value`). */
  label?: string;
  withDot?: boolean;
  className?: string;
};

/**
 * The one status chip used everywhere. Pass a `tone` (from a domain helper in
 * lib/ui/status.ts) or a raw `value` to auto-resolve. Consistent shape, size,
 * and colors in light and dark.
 */
export function StatusBadge({
  tone,
  value,
  label,
  withDot = true,
  className,
}: StatusBadgeProps) {
  const resolved = tone ?? statusTone(value);
  const text = label ?? (value ? value.replace(/_/g, " ") : "—");

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium capitalize whitespace-nowrap",
        TONE_CLASSES[resolved],
        className,
      )}
    >
      {withDot && (
        <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", TONE_DOT[resolved])} />
      )}
      {text}
    </span>
  );
}
