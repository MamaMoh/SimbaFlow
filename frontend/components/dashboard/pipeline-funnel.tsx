"use client";

import Link from "next/link";
import type { PipelineFunnelStage } from "@/lib/api/dashboard";
import { Loader2 } from "lucide-react";

/**
 * How many candidates are standing in each stage.
 *
 * The card fills its column rather than setting its own height. It sits beside the trend chart,
 * and an agency with nine stages made this one half as tall again as the chart — two cards of
 * different heights on the same row, which is the thing that made the dashboard look unfinished.
 */
export function PipelineFunnel({
  stages,
  isLoading,
}: {
  stages: PipelineFunnelStage[];
  isLoading?: boolean;
}) {
  if (isLoading) {
    return (
      <div className="flex h-full items-center rounded-lg border bg-card p-6 text-sm text-muted-foreground shadow-sm">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading pipeline funnel…
      </div>
    );
  }

  const max = Math.max(1, ...stages.map((s) => s.count));

  return (
    <div className="flex h-full flex-col rounded-lg border bg-card p-5 shadow-sm">
      <div className="mb-4">
        <h2 className="text-sm font-semibold">Pipeline funnel</h2>
        <p className="text-xs text-muted-foreground">Candidates by stage</p>
      </div>

      {stages.length === 0 ? (
        <p className="flex flex-1 items-center text-sm text-muted-foreground">
          No workflow stages found.
        </p>
      ) : (
        // Spread down the card, so the rows end level with the chart beside them however many
        // stages this agency runs.
        <ul className="flex flex-1 flex-col justify-between gap-2.5">
          {stages.map((stage) => {
            const pct = Math.round((stage.count / max) * 100);
            return (
              <li key={stage.stageId}>
                <div className="flex items-center justify-between gap-3 text-[13px]">
                  <Link
                    href={`/candidates?stage=${stage.stageId}`}
                    className="truncate font-medium underline-offset-2 hover:underline"
                  >
                    {stage.stageName}
                    {stage.isFinalStage ? (
                      <span className="font-normal text-muted-foreground"> (final)</span>
                    ) : null}
                  </Link>
                  <span className="shrink-0 tabular-nums text-muted-foreground">
                    {stage.count}
                  </span>
                </div>
                <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-green-800/80 transition-[width]"
                    style={{ width: `${pct}%` }}
                  />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
