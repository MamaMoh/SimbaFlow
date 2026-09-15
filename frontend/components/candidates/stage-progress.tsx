"use client";

import { Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { useWorkflowDefinition } from "@/lib/api/workflow";

/**
 * Where this candidate has got to, across the whole pipeline.
 *
 * The stage badge alone says where someone is but not how far along that is, and the pipeline is
 * the thing everyone in the agency talks in — "she's at embassy", "he's waiting on a ticket". The
 * stages come from the workflow definition rather than a hardcoded list, because agencies
 * reconfigure them.
 */
export function StageProgress({
  currentStageId,
  currentStageName,
}: {
  currentStageId?: string | null;
  currentStageName?: string | null;
}) {
  const { stages } = useWorkflowDefinition();

  if (!stages.length) return null;

  const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);

  // Match on id where we have one; the boards pass a name, so fall back to that.
  const currentIndex = ordered.findIndex((s) =>
    currentStageId
      ? s.id === currentStageId
      : s.name.toLowerCase() === (currentStageName ?? "").toLowerCase()
  );

  return (
    <nav
      aria-label="Pipeline progress"
      className="overflow-x-auto rounded-xl border bg-card px-4 py-3 shadow-sm"
    >
      <ol className="flex min-w-max items-center gap-1">
        {ordered.map((stage, index) => {
          const done = currentIndex >= 0 && index < currentIndex;
          const here = index === currentIndex;

          return (
            <li key={stage.id} className="flex items-center gap-1">
              <div
                aria-current={here ? "step" : undefined}
                className={cn(
                  "flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium whitespace-nowrap transition-colors",
                  here && "bg-emerald-700 text-white shadow-sm",
                  done && "bg-emerald-50 text-emerald-800",
                  !here && !done && "text-muted-foreground"
                )}
              >
                {done ? (
                  <Check className="h-3 w-3 shrink-0" aria-hidden="true" />
                ) : (
                  <span
                    className={cn(
                      "flex h-4 w-4 shrink-0 items-center justify-center rounded-full text-[10px] font-semibold",
                      here ? "bg-white/20 text-white" : "bg-muted text-muted-foreground"
                    )}
                    aria-hidden="true"
                  >
                    {index + 1}
                  </span>
                )}
                {stage.name}
                {done && <span className="sr-only">(completed)</span>}
                {here && <span className="sr-only">(current stage)</span>}
              </div>

              {index < ordered.length - 1 && (
                <span
                  aria-hidden="true"
                  className={cn(
                    "h-px w-5 shrink-0",
                    index < currentIndex ? "bg-emerald-300" : "bg-border"
                  )}
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
