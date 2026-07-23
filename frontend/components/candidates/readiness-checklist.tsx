"use client";

import { Check, Circle } from "lucide-react";
import type { CandidateReadiness } from "@/lib/demo/clearances";
import { cn } from "@/lib/utils";

interface ReadinessChecklistProps {
  readiness: CandidateReadiness;
}

export function ReadinessChecklist({ readiness }: ReadinessChecklistProps) {
  const { percent, completed, total, items } = readiness;

  return (
    <div className="rounded-xl border bg-card p-4 shadow-sm">
      <div className="mb-3 flex items-end justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            Departure readiness
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {completed} of {total} complete
          </p>
        </div>
        <div className="text-right">
          <div className="text-2xl font-bold tabular-nums tracking-tight">{percent}%</div>
        </div>
      </div>

      <div className="mb-4 h-2 overflow-hidden rounded-full bg-muted">
        <div
          className={cn(
            "h-full rounded-full transition-all duration-500",
            percent >= 100 ? "bg-emerald-500" : percent >= 60 ? "bg-primary" : "bg-amber-500",
          )}
          style={{ width: `${percent}%` }}
        />
      </div>

      <ul className="space-y-2">
        {items.map((item) => (
          <li
            key={item.id}
            className={cn(
              "flex items-start gap-2.5 rounded-md px-2 py-1.5 text-sm",
              item.done ? "bg-emerald-50/50" : "bg-muted/40",
            )}
          >
            {item.done ? (
              <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
            ) : (
              <Circle className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground/50" />
            )}
            <div className="min-w-0 flex-1">
              <div className={cn("font-medium", item.done && "text-emerald-900")}>{item.label}</div>
              {item.detail && (
                <div className="truncate text-[11px] text-muted-foreground">{item.detail}</div>
              )}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
