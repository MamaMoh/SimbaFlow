"use client";

import Link from "next/link";
import type { PipelineFunnelStage } from "@/lib/api/dashboard";
import { Loader2 } from "lucide-react";

export function PipelineFunnel({
  stages,
  isLoading,
}: {
  stages: PipelineFunnelStage[];
  isLoading?: boolean;
}) {
  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-6 shadow-sm flex items-center text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin mr-2" />
        Loading pipeline funnel…
      </div>
    );
  }

  const max = Math.max(1, ...stages.map((s) => s.count));

  return (
    <div className="rounded-lg border bg-card p-6 shadow-sm space-y-4">
      <div>
        <h2 className="text-lg font-semibold">Pipeline funnel</h2>
        <p className="text-sm text-muted-foreground">
          Active candidates by current stage
        </p>
      </div>

      {stages.length === 0 ? (
        <p className="text-sm text-muted-foreground">No workflow stages found.</p>
      ) : (
        <ul className="space-y-3">
          {stages.map((stage) => {
            const pct = Math.round((stage.count / max) * 100);
            return (
              <li key={stage.stageId} className="space-y-1">
                <div className="flex items-center justify-between gap-3 text-sm">
                  <Link
                    href={`/candidates?stage=${stage.stageId}`}
                    className="font-medium hover:underline underline-offset-2 truncate"
                  >
                    {stage.stageName}
                    {stage.isFinalStage ? (
                      <span className="text-muted-foreground font-normal">
                        {" "}
                        (final)
                      </span>
                    ) : null}
                  </Link>
                  <span className="tabular-nums text-muted-foreground shrink-0">
                    {stage.count}
                  </span>
                </div>
                <div className="h-2 rounded-full bg-muted overflow-hidden">
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
