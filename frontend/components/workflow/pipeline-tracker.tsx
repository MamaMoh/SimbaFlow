"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";
import { DEMO_STAGES, getPipelineCounts } from "@/lib/demo/demo-data";
import { ChevronRight } from "lucide-react";

export function PipelineTracker({ activeSlug }: { activeSlug?: string }) {
  const counts = getPipelineCounts();

  return (
    <div className="rounded-xl border bg-card/80 backdrop-blur-sm p-3 shadow-sm">
      <div className="flex items-center gap-1 overflow-x-auto pb-1">
        {counts.map((stage, i) => {
          const active = activeSlug === stage.slug;
          return (
            <div key={stage.id} className="flex items-center gap-1 shrink-0">
              <Link
                href={`/workflow/${stage.slug}`}
                className={cn(
                  "group flex min-w-[118px] flex-col rounded-lg border px-3 py-2 transition-all",
                  active
                    ? "border-primary/40 bg-primary/5 shadow-sm ring-1 ring-primary/20"
                    : "border-transparent hover:border-border hover:bg-muted/50",
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <span
                    className={cn(
                      "text-[11px] font-semibold uppercase tracking-wide",
                      active ? "text-primary" : "text-muted-foreground",
                    )}
                  >
                    {stage.name}
                  </span>
                  {stage.overdue > 0 && (
                    <span className="rounded-full bg-rose-100 px-1.5 text-[10px] font-bold text-rose-700">
                      {stage.overdue}
                    </span>
                  )}
                </div>
                <div className="mt-1 flex items-baseline gap-1">
                  <span className="text-xl font-bold tabular-nums tracking-tight">{stage.count}</span>
                  <span className="text-[10px] text-muted-foreground">in stage</span>
                </div>
                <div className="mt-2 h-1 overflow-hidden rounded-full bg-muted">
                  <div
                    className={cn("h-full rounded-full transition-all", active ? "bg-primary" : "bg-slate-300")}
                    style={{ width: `${Math.min(100, stage.count * 12)}%` }}
                  />
                </div>
              </Link>
              {i < DEMO_STAGES.length - 1 && (
                <ChevronRight className="h-4 w-4 text-muted-foreground/50 shrink-0" />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
