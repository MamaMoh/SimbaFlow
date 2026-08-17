"use client";

import { useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  resolveStageFromSlugOrId,
  useViewCandidates,
  useWorkflowDefinition,
} from "@/lib/api/workflow";
import { WorkflowViewTable } from "@/components/workflow/workflow-view-table";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { Input } from "@/components/ui/input";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";

export default function WorkflowViewPage() {
  const { stageId: stageParam } = useParams<{ stageId: string }>();
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView =
    hasPermission("workflow.view") || hasPermission("system.admin");
  const [search, setSearch] = useState("");

  const {
    stages,
    definition,
    isLoading: loadingDef,
    error: defError,
    mutate: mutateDef,
  } = useWorkflowDefinition();

  const stage = useMemo(
    () => resolveStageFromSlugOrId(stages, stageParam),
    [stages, stageParam]
  );

  const {
    candidates,
    totalCount,
    isLoading: loadingView,
    error: viewError,
    mutate,
  } = useViewCandidates(stage?.id, { search: search || undefined, pageSize: 50 });

  const statusTracks = useMemo(() => {
    if (!stage) return [];
    const fromTracks = stage.parallelTracks?.map((t) => t.trackName) ?? [];
    const fromStatuses = [
      ...new Set(
        (stage.statuses ?? [])
          .map((s) => s.trackName)
          .filter((t): t is string => !!t)
      ),
    ];
    return fromTracks.length > 0 ? fromTracks : fromStatuses;
  }, [stage]);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) {
    return <AccessDenied resource="workflow boards" />;
  }

  if (loadingDef) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading workflow…
      </div>
    );
  }

  if (defError) {
    return (
      <div className="p-6">
        <LoadError
          message={
            defError instanceof Error
              ? defError.message
              : "Failed to load workflow configuration. Ensure the tenant has a seeded default workflow."
          }
          onRetry={() => mutateDef()}
        />
      </div>
    );
  }

  if (!stage) {
    return (
      <div className="p-6 space-y-4">
        <h1 className="text-2xl font-bold tracking-tight">Workflow View</h1>
        <PageAlert
          variant="error"
          title={`Unknown stage “${stageParam}”`}
          description={`Available stages: ${stages.map((s) => s.name).join(", ") || "none"}`}
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={stage.name}
        description={<>{definition?.name ?? "Workflow"} · {totalCount} candidate
{totalCount === 1 ? "" : "s"}
{stage.description ? ` · ${stage.description}` : ""}</>}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search in this stage…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        }
      />

      {viewError && (
        <LoadError
          message={viewError instanceof Error ? viewError.message : String(viewError)}
          onRetry={() => mutate()}
        />
      )}


      {stage.name.toLowerCase().includes("new contract") && (
        <PageAlert
          variant="info"
          title="To Embassy unlocks after Mark Ready"
          description="Review the candidate, click Mark Ready, then To Embassy becomes active."
        />
      )}

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        {loadingView ? (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin mr-2" />
            Loading candidates…
          </div>
        ) : (
          <WorkflowViewTable
            candidates={candidates}
            onMutate={() => mutate()}
            statusTracks={statusTracks}
          />
        )}
      </div>
    </div>
  );
}
