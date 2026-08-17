"use client";

import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { StageEditor } from "@/components/workflow/stage-editor";
import { CreateTransitionSheet } from "@/components/workflow/create-transition-sheet";
import { useWorkflowDefinition } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import type { ConditionGroup, WorkflowStage, WorkflowTransitionRule } from "@/types/workflow";
import { Loader2, Pencil, Plus } from "lucide-react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TransitionEditor } from "@/components/workflow/transition-editor";
import { StatusBadge } from "@/components/ui/status-badge";
import { toast } from "sonner";

const STAGE_TYPE_LABEL: Record<number, string> = {
  0: "Simple",
  1: "Parallel",
  2: "Milestone",
};

function formatConditions(raw: unknown): string {
  if (!raw || typeof raw !== "object") return "None";
  const g = raw as ConditionGroup;
  if (!g.rules?.length) return "None";
  return g.rules
    .map((r) => {
      const val = Array.isArray(r.value) ? r.value.join("|") : (r.value ?? "");
      return `${r.field} ${r.op} ${val}`.trim();
    })
    .join(` ${g.operator || "AND"} `);
}

export function WorkflowConfigEditor() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const { definition, stages, isLoading, error, mutate } = useWorkflowDefinition();
  const [stageEditorOpen, setStageEditorOpen] = useState(false);
  const [editingStage, setEditingStage] = useState<WorkflowStage | null>(null);
  const [transitionOpen, setTransitionOpen] = useState(false);
  const [defaultSourceId, setDefaultSourceId] = useState<string | undefined>();
  const [editingTransition, setEditingTransition] =
    useState<WorkflowTransitionRule | null>(null);

  /** Soft-delete a step (transition) after confirmation. */
  const deleteTransition = async (t: WorkflowTransitionRule) => {
    if (!window.confirm(`Remove the step “${t.buttonLabel}”?`)) return;
    try {
      const res = await fetch(
        `/api/proxy/workflow/config/transitions/${t.id}`,
        { method: "DELETE" },
      );
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) {
        throw new Error(body?.error || "Failed to remove step");
      }
      toast.success("Step removed");
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to remove step");
    }
  };

  /** Retire a stage; backend refuses if candidates still sit in it. */
  const deleteStage = async (stage: WorkflowStage) => {
    if (!window.confirm(`Remove the step “${stage.name}” from this workflow?`)) return;
    try {
      const res = await fetch(`/api/proxy/workflow/config/stages/${stage.id}`, {
        method: "DELETE",
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) {
        throw new Error(body?.error || "Failed to remove step");
      }
      toast.success("Step removed");
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to remove step");
    }
  };

  const stageNameById = useMemo(() => {
    const map = new Map<string, string>();
    stages.forEach((s) => map.set(s.id, s.name));
    return map;
  }, [stages]);

  const transitions: WorkflowTransitionRule[] = definition?.transitionRules ?? [];

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!hasPermission("workflow.configure") && !hasPermission("system.admin")) {
    return <AccessDenied resource="workflow configuration" />;
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading workflow configuration…
      </div>
    );
  }

  if (error) {
    return (
      <LoadError
        message={error instanceof Error ? error.message : String(error)}
        onRetry={() => mutate()}
      />
    );
  }

  if (!definition) {
    return (
      <PageAlert
        variant="error"
        title="No workflow definition"
        description="This tenant has no active workflow. Provision or seed the default pipeline first."
      />
    );
  }

  const nextSort =
    stages.length > 0 ? Math.max(...stages.map((s) => s.sortOrder)) + 1 : 0;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">Workflow Config</h1>
          <p className="text-sm text-muted-foreground">
            {definition.name}
            {definition.description ? ` — ${definition.description}` : ""} · v
            {definition.version}
            {definition.isActive ? (
              <Badge className="ml-2 bg-green-100 text-green-800 hover:bg-green-100">
                Active
              </Badge>
            ) : (
              <Badge variant="secondary" className="ml-2">
                Inactive
              </Badge>
            )}
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setDefaultSourceId(undefined);
              setTransitionOpen(true);
            }}
          >
            <Plus className="h-4 w-4 mr-1" /> Transition
          </Button>
          <Button
            size="sm"
            className="bg-green-800 hover:bg-green-900 text-white"
            onClick={() => {
              setEditingStage(null);
              setStageEditorOpen(true);
            }}
          >
            <Plus className="h-4 w-4 mr-1" /> Stage
          </Button>
        </div>
      </div>

      <section className="rounded-lg border bg-card p-4 shadow-sm space-y-3">
        <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          Stages ({stages.length})
        </h2>
        {stages.length === 0 ? (
          <PageAlert
            variant="info"
            title="No stages"
            description="Add stages to build the candidate pipeline."
          />
        ) : (
          <div className="divide-y rounded-md border">
            {stages.map((stage) => (
              <div
                key={stage.id}
                className="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{stage.name}</span>
                    <Badge variant="outline">{STAGE_TYPE_LABEL[stage.stageType] ?? "—"}</Badge>
                    {stage.isInitialStage && (
                      <Badge variant="secondary">Initial</Badge>
                    )}
                    {stage.isFinalStage && (
                      <Badge variant="secondary">Final</Badge>
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Order {stage.sortOrder}
                    {stage.description ? ` · ${stage.description}` : ""}
                    {stage.parallelTracks?.length
                      ? ` · ${stage.parallelTracks.length} track(s)`
                      : ""}
                    {stage.statuses?.length
                      ? ` · ${stage.statuses.length} status(es)`
                      : ""}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      setDefaultSourceId(stage.id);
                      setTransitionOpen(true);
                    }}
                  >
                    + Transition
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-destructive hover:text-destructive"
                    onClick={() => void deleteStage(stage)}
                  >
                    Remove
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setEditingStage(stage);
                      setStageEditorOpen(true);
                    }}
                  >
                    <Pencil className="h-3.5 w-3.5 mr-1" /> Edit
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="rounded-lg border bg-card p-4 shadow-sm space-y-3">
        <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          Transitions ({transitions.length})
        </h2>
        {transitions.length === 0 ? (
          <PageAlert
            variant="info"
            title="No transitions"
            description="Add transitions so staff can move candidates between stages."
          />
        ) : (
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Button</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Who can do it</TableHead>
                  <TableHead>Conditions</TableHead>
                  <TableHead>Active</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y">
                {transitions.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>{t.buttonLabel}</TableCell>
                    <TableCell>
                      {stageNameById.get(t.sourceStageId) ?? t.sourceStageId}
                    </TableCell>
                    <TableCell>
                      {stageNameById.get(t.targetStageId) ?? t.targetStageId}
                    </TableCell>
                    <TableCell>
                      {t.allowedRoles && t.allowedRoles.length > 0 ? (
                        <div className="flex flex-wrap gap-1">
                          {t.allowedRoles.map((r) => (
                            <StatusBadge
                              key={r}
                              tone="info"
                              label={r.replace(/([A-Z])/g, " $1").trim()}
                              withDot={false}
                            />
                          ))}
                        </div>
                      ) : (
                        <span className="text-xs text-muted-foreground">
                          Anyone with access
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground max-w-xs truncate">
                      {formatConditions(t.conditions)}
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        tone={t.isActive ? "success" : "neutral"}
                        label={t.isActive ? "Yes" : "No"}
                      />
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => setEditingTransition(t)}
                      >
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-destructive hover:text-destructive"
                        onClick={() => void deleteTransition(t)}
                      >
                        Remove
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </section>

      <TransitionEditor
        open={!!editingTransition}
        onOpenChange={(o) => {
          if (!o) setEditingTransition(null);
        }}
        transition={editingTransition}
        fromName={
          editingTransition
            ? stageNameById.get(editingTransition.sourceStageId)
            : undefined
        }
        toName={
          editingTransition
            ? stageNameById.get(editingTransition.targetStageId)
            : undefined
        }
        onSaved={() => mutate()}
      />

      <StageEditor
        open={stageEditorOpen}
        onOpenChange={(o) => {
          setStageEditorOpen(o);
          if (!o) setEditingStage(null);
        }}
        stage={editingStage}
        defaultSortOrder={nextSort}
        onSaved={() => mutate()}
      />
      <CreateTransitionSheet
        open={transitionOpen}
        onOpenChange={setTransitionOpen}
        stages={stages}
        defaultSourceId={defaultSourceId}
        onCreated={() => mutate()}
      />
    </div>
  );
}
