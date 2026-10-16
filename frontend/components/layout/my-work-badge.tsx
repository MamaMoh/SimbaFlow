"use client";

import { totalNeedsAction, useMyWorkQueues } from "@/lib/api/work-queues";
import { cn } from "@/lib/utils";

/**
 * How many rows across My work's tabs are this desk's turn, beside the link in the sidebar.
 *
 * Only what needs action is counted: a visa sitting with the embassy is on the page to keep an
 * eye on, but a number that never goes down however much work gets done stops being read.
 */
export function MyWorkBadge({ collapsed }: { collapsed?: boolean }) {
  const { data } = useMyWorkQueues();
  const count = totalNeedsAction(data);
  if (count === 0) return null;

  if (collapsed) {
    return (
      <span
        aria-label={`${count} waiting`}
        className="absolute right-1 top-1 h-2 w-2 rounded-full bg-primary"
      />
    );
  }

  return (
    <span
      className={cn(
        "ml-auto inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5",
        "bg-primary text-[11px] font-semibold leading-none text-primary-foreground tabular-nums"
      )}
    >
      {count > 99 ? "99+" : count}
    </span>
  );
}
