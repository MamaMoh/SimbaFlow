"use client";

import type { TimelineEntry } from "@/types/candidate";

const EVENT_LABELS: Record<number, string> = {
  0: "Registered",
  1: "Stage changed",
  2: "Status updated",
  3: "Field updated",
  4: "Action executed",
  5: "Mirror activated",
  6: "Mirror deactivated",
  7: "Exception flagged",
  8: "Archived",
};

type CandidateTimelineProps = {
  events: TimelineEntry[];
};

export function CandidateTimeline({ events }: CandidateTimelineProps) {
  if (events.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-6 text-center">
        No workflow events yet.
      </p>
    );
  }

  return (
    <ol className="relative space-y-0 border-l border-border ml-3">
      {events.map((evt) => {
        const label = EVENT_LABELS[evt.eventType] ?? `Event ${evt.eventType}`;
        const stageLine =
          evt.fromStageName || evt.toStageName
            ? `${evt.fromStageName ?? "—"} → ${evt.toStageName ?? "—"}`
            : null;

        return (
          <li key={evt.id} className="mb-6 ml-6">
            <span className="absolute -left-1.5 mt-1.5 h-3 w-3 rounded-full border border-background bg-primary" />
            <div className="rounded-md border bg-card px-3 py-2">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <p className="text-sm font-medium">{label}</p>
                <time className="text-xs text-muted-foreground">
                  {new Date(evt.timestamp).toLocaleString()}
                </time>
              </div>
              {stageLine && (
                <p className="text-sm text-muted-foreground mt-0.5">{stageLine}</p>
              )}
              <p className="text-xs text-muted-foreground mt-1">
                by {evt.userName}
                {evt.notes ? ` · ${evt.notes}` : ""}
              </p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
