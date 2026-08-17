"use client";

import { cn } from "@/lib/utils";
import { StatusBadge } from "@/components/ui/status-badge";
import { TRACK_LABELS, isPrimaryTrack, statusTone } from "@/lib/ui/status";

type CandidateStatusBadgeProps = {
  stageName?: string | null;
  statusValues?: Record<string, string> | null;
  isMirror?: boolean;
  className?: string;
};

/**
 * Candidate stage + track chips. Delegates all status coloring to the canonical
 * StatusBadge / status.ts so it matches the rest of the app.
 */
export function CandidateStatusBadge({
  stageName,
  statusValues,
  isMirror,
  className,
}: CandidateStatusBadgeProps) {
  const entries = Object.entries(statusValues ?? {}).filter(
    ([key, value]) => value && isPrimaryTrack(key),
  );

  return (
    <div className={cn("flex flex-wrap items-center gap-1.5", className)}>
      <span className="inline-flex items-center rounded-full border border-transparent bg-amber-400/90 px-2 py-0.5 text-xs font-semibold text-amber-950">
        {stageName || "—"}
      </span>
      {isMirror && <StatusBadge tone="progress" label="Mirror" withDot={false} />}
      {entries.map(([track, value]) => {
        const label = TRACK_LABELS[track.toLowerCase()] ?? track;
        return (
          <StatusBadge
            key={track}
            tone={statusTone(value)}
            label={`${label}: ${value.replace(/_/g, " ")}`}
            withDot={false}
          />
        );
      })}
    </div>
  );
}
