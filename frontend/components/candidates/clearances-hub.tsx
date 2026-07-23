"use client";

import Link from "next/link";
import { ExternalLink, Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import { StatusPill, FlagBadge } from "@/components/workflow/status-pill";
import type { CandidateClearance } from "@/lib/demo/clearances";
import { cn } from "@/lib/utils";

interface ClearancesHubProps {
  clearances: CandidateClearance[];
  onMarkDone?: (serviceId: string) => void;
  canMutate?: boolean;
}

export function ClearancesHub({ clearances, onMarkDone, canMutate }: ClearancesHubProps) {
  if (!clearances.length) {
    return (
      <div className="rounded-xl border bg-card p-4 shadow-sm">
        <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Clearances
        </h2>
        <p className="text-sm text-muted-foreground">No clearance data</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border bg-card p-4 shadow-sm">
      <div className="mb-4 flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Clearances
        </h2>
        <FlagBadge tone="neutral">EasyEnjaz-style</FlagBadge>
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {clearances.map((c) => {
          const complete = c.status === "Complete";
          return (
            <div
              key={c.serviceId}
              className={cn(
                "flex flex-col rounded-lg border p-3 transition-colors",
                c.blocking && "border-rose-200 bg-rose-50/40",
                complete && "border-emerald-200/80 bg-emerald-50/30",
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="text-sm font-semibold">{c.easyenjazAlias}</div>
                  <div className="text-[11px] text-muted-foreground">{c.label}</div>
                </div>
                <StatusPill value={c.statusLabel} size="sm" />
              </div>
              <p className="mt-2 flex-1 text-xs text-muted-foreground">{c.description}</p>
              {c.since && (
                <p className="mt-1 text-[10px] text-muted-foreground">
                  Updated {new Date(c.since).toLocaleString()}
                </p>
              )}
              <div className="mt-3 flex flex-wrap gap-1.5">
                {canMutate && !complete && (
                  <Button
                    size="sm"
                    variant="default"
                    className="h-7 gap-1 text-xs"
                    onClick={() => onMarkDone?.(c.serviceId)}
                  >
                    <Check className="h-3 w-3" />
                    Mark done
                  </Button>
                )}
                <Button asChild size="sm" variant="outline" className="h-7 gap-1 text-xs">
                  <Link href={c.href}>
                    Open stage
                    <ExternalLink className="h-3 w-3" />
                  </Link>
                </Button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
