"use client";

import { use } from "react";
import useSWR from "swr";
import Link from "next/link";
import { candidatesApi, USE_MOCKS } from "@/lib/api/candidates-api";
import { PipelineTracker } from "@/components/workflow/pipeline-tracker";
import { StatusPill } from "@/components/workflow/status-pill";
import { ContentLoading } from "@/components/loading/loading-components";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ArrowLeft, Clock3 } from "lucide-react";

export default function CandidateDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useSWR(["candidate", id], () => candidatesApi.getById(id), {
    revalidateOnFocus: false,
  });
  const { data: timelineRes } = useSWR(["timeline", id], () => candidatesApi.timeline(id), {
    revalidateOnFocus: false,
  });

  const c = data?.data as any;
  const timeline = timelineRes?.data ?? [];

  if (isLoading) {
    return <ContentLoading text="Loading candidate…" className="min-h-[50vh]" />;
  }

  if (!c) {
    return (
      <div className="p-8">
        <p className="text-muted-foreground">Candidate not found</p>
        <Button asChild variant="outline" className="mt-4">
          <Link href="/candidates">Back</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <PipelineTracker />

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button asChild variant="ghost" size="sm" className="mb-2 -ml-2 gap-1">
            <Link href="/candidates">
              <ArrowLeft className="h-4 w-4" /> Candidates
            </Link>
          </Button>
          <h1 className="text-2xl font-bold tracking-tight">{c.fullName}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <span className="font-mono">{c.passportNumber}</span>
            <span>·</span>
            <span>{c.applicationNo || c.labourId}</span>
            {c.isOverdue && <Badge variant="destructive">Overdue</Badge>}
            {USE_MOCKS && <Badge variant="outline">Demo</Badge>}
          </div>
        </div>
        <div className="flex items-center gap-2 rounded-lg border bg-card px-3 py-2 shadow-sm">
          <StatusPill value={c.currentStageName || c.stageSlug} />
          <span className="inline-flex items-center gap-1 text-xs font-bold text-muted-foreground">
            <Clock3 className="h-3.5 w-3.5" /> {c.daysInStage ?? 0}d in stage
          </span>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <section className="lg:col-span-2 space-y-4">
          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">Profile</h2>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              {[
                ["Labour ID", c.labourId],
                ["Destination", c.countryOfTravel],
                ["Office", c.officeName],
                ["Phone", c.phoneNumber],
                ["Nationality", c.nationality],
                ["Registered", c.registeredAt ? new Date(c.registeredAt).toLocaleString() : "—"],
                ["Last action", c.lastActionLabel],
                ["Entered stage", c.enteredAt || c.currentStageEnteredAt ? new Date(c.enteredAt || c.currentStageEnteredAt).toLocaleString() : "—"],
              ].map(([k, v]) => (
                <div key={k as string}>
                  <dt className="text-[11px] uppercase text-muted-foreground">{k}</dt>
                  <dd className="font-medium">{(v as string) || "—"}</dd>
                </div>
              ))}
            </dl>
          </div>

          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Placement
            </h2>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              {[
                ["Sponsor", c.placement?.sponsorName || c.sponsorName],
                ["Sponsor ID", c.placement?.sponsorId || c.sponsorId],
                ["Visa #", c.placement?.visaNumber || c.visaNo],
                ["Agent", c.placement?.agent || c.agent],
              ].map(([k, v]) => (
                <div key={k as string}>
                  <dt className="text-[11px] uppercase text-muted-foreground">{k}</dt>
                  <dd className="font-medium">{(v as string) || "—"}</dd>
                </div>
              ))}
            </dl>
          </div>

          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
              Current status tracks
            </h2>
            <div className="flex flex-wrap gap-2">
              {Object.entries(c.currentStatusValues || c.statusValues || {})
                .filter(([k]) => !k.endsWith("_since") && k !== "tasheer_datetime")
                .map(([k, v]) => (
                  <StatusPill key={k} label={k} value={String(v)} />
                ))}
              {!Object.keys(c.currentStatusValues || c.statusValues || {}).length && (
                <span className="text-sm text-muted-foreground">No status values yet</span>
              )}
            </div>
          </div>
        </section>

        <section className="rounded-xl border bg-card p-4 shadow-sm">
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            Timeline
          </h2>
          <ol className="relative space-y-4 border-l border-border ml-2 pl-4">
            {timeline.map((ev: any) => (
              <li key={ev.id} className="relative">
                <span className="absolute -left-[21px] top-1 h-2.5 w-2.5 rounded-full bg-primary ring-4 ring-background" />
                <div className="text-xs text-muted-foreground">
                  {new Date(ev.timestamp).toLocaleString()}
                </div>
                <div className="text-sm font-semibold">{ev.eventTypeName}</div>
                <div className="text-xs text-muted-foreground">
                  {[ev.fromStageName, ev.toStageName].filter(Boolean).join(" → ")}
                  {ev.toStatus ? ` · ${ev.trackKey}: ${ev.toStatus}` : ""}
                  {ev.durationLabel ? ` · ${ev.durationLabel}` : ""}
                </div>
                <div className="text-[11px] text-muted-foreground">by {ev.userName}</div>
                {ev.notes && <div className="mt-1 text-xs">{ev.notes}</div>}
              </li>
            ))}
          </ol>
        </section>
      </div>
    </div>
  );
}
