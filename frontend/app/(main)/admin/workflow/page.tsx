"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import {
  ArrowDown,
  ArrowUp,
  Clock3,
  GitBranch,
  Pencil,
  Plus,
  RotateCcw,
  Settings2,
  Trash2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { FlagBadge, StatusPill } from "@/components/workflow/status-pill";
import { StageFormSheet } from "@/components/workflow/admin/stage-form-sheet";
import { TransitionFormSheet } from "@/components/workflow/admin/transition-form-sheet";
import {
  deleteStage,
  deleteTransition,
  getWorkflowConfig,
  moveStage,
  resetWorkflowConfig,
  subscribeWorkflowConfig,
  toggleTransitionActive,
  updateWorkflowMeta,
} from "@/lib/demo/workflow-config-store";
import type { WorkflowDefinition, WorkflowStage, WorkflowTransitionRule } from "@/types/workflow";
import { cn } from "@/lib/utils";
import { USE_MOCKS } from "@/lib/api/candidates-api";

const STAGE_TYPE_LABEL: Record<number, string> = {
  0: "Simple",
  1: "Parallel tracks",
  2: "Milestones",
};

export default function WorkflowConfigPage() {
  const [def, setDef] = useState<WorkflowDefinition>(() => getWorkflowConfig());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [stageSheetOpen, setStageSheetOpen] = useState(false);
  const [editingStage, setEditingStage] = useState<WorkflowStage | null>(null);
  const [transitionSheetOpen, setTransitionSheetOpen] = useState(false);
  const [editingTransition, setEditingTransition] = useState<WorkflowTransitionRule | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<
    | { type: "stage"; id: string; name: string }
    | { type: "transition"; id: string; name: string }
    | null
  >(null);
  const [metaName, setMetaName] = useState(def.name);
  const [metaDescription, setMetaDescription] = useState(def.description ?? "");

  const refresh = useCallback(() => {
    const next = getWorkflowConfig();
    setDef(next);
    setMetaName(next.name);
    setMetaDescription(next.description ?? "");
  }, []);

  useEffect(() => subscribeWorkflowConfig(refresh), [refresh]);

  const stages = useMemo(
    () => [...def.stages].sort((a, b) => a.sortOrder - b.sortOrder),
    [def.stages],
  );

  const selected = stages.find((s) => s.id === selectedId) ?? stages[0] ?? null;

  useEffect(() => {
    if (!selectedId && stages[0]) setSelectedId(stages[0].id);
  }, [selectedId, stages]);

  const stageName = (id: string) => stages.find((s) => s.id === id)?.name ?? id;

  const relatedTransitions = useMemo(() => {
    if (!selected) return [];
    return def.transitionRules.filter(
      (t) => t.sourceStageId === selected.id || t.targetStageId === selected.id,
    );
  }, [def.transitionRules, selected]);

  const saveMeta = () => {
    updateWorkflowMeta({ name: metaName, description: metaDescription });
    toast.success("Pipeline settings saved");
  };

  const confirmDelete = () => {
    if (!deleteTarget) return;
    if (deleteTarget.type === "stage") {
      deleteStage(deleteTarget.id);
      if (selectedId === deleteTarget.id) setSelectedId(null);
      toast.success("Stage removed");
    } else {
      deleteTransition(deleteTarget.id);
      toast.success("Transition removed");
    }
    setDeleteTarget(null);
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Workflow configuration</h1>
          <p className="text-sm text-muted-foreground">
            Configure stages, SLAs, tracks, and transition actions
            {USE_MOCKS ? " · demo data (actions work in-session)" : ""}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <FlagBadge tone="neutral">v{def.version}</FlagBadge>
          <div className="flex items-center gap-2 rounded-lg border bg-card px-3 py-1.5 text-sm">
            <span className="text-muted-foreground">Active</span>
            <Switch
              checked={def.isActive}
              onCheckedChange={(v) => {
                updateWorkflowMeta({ isActive: v });
                toast.success(v ? "Pipeline activated" : "Pipeline deactivated");
              }}
            />
          </div>
          <Button
            variant="outline"
            size="sm"
            className="gap-1"
            onClick={() => {
              resetWorkflowConfig();
              setSelectedId(null);
              toast.message("Reset to seeded pipeline");
            }}
          >
            <RotateCcw className="h-3.5 w-3.5" /> Reset
          </Button>
          <Button
            size="sm"
            className="gap-1"
            onClick={() => {
              setEditingStage(null);
              setStageSheetOpen(true);
            }}
          >
            <Plus className="h-3.5 w-3.5" /> Add stage
          </Button>
        </div>
      </div>

      <Tabs defaultValue="pipeline" className="w-full">
        <TabsList>
          <TabsTrigger value="pipeline">Pipeline</TabsTrigger>
          <TabsTrigger value="transitions">Transitions</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="pipeline" className="mt-4">
          <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
            <aside className="rounded-xl border bg-card p-3 shadow-sm">
              <div className="mb-3 flex items-center justify-between px-1">
                <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Stages ({stages.length})
                </h2>
              </div>
              <ol className="space-y-1.5">
                {stages.map((s, i) => {
                  const active = selected?.id === s.id;
                  return (
                    <li key={s.id}>
                      <button
                        type="button"
                        onClick={() => setSelectedId(s.id)}
                        className={cn(
                          "flex w-full items-start gap-3 rounded-lg border px-3 py-2.5 text-left transition-colors",
                          active
                            ? "border-primary/40 bg-primary/5 shadow-sm"
                            : "border-transparent hover:bg-muted/50",
                        )}
                      >
                        <span
                          className={cn(
                            "mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold",
                            active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground",
                          )}
                        >
                          {i + 1}
                        </span>
                        <div className="min-w-0 flex-1">
                          <div className="truncate font-semibold">{s.name}</div>
                          <div className="mt-0.5 flex flex-wrap gap-1">
                            <span className="text-[10px] text-muted-foreground">
                              {STAGE_TYPE_LABEL[s.stageType] ?? "Simple"}
                            </span>
                            {s.isInitialStage && <FlagBadge tone="info">Start</FlagBadge>}
                            {s.isFinalStage && <FlagBadge tone="warning">End</FlagBadge>}
                          </div>
                        </div>
                      </button>
                    </li>
                  );
                })}
              </ol>
            </aside>

            <section className="rounded-xl border bg-card p-4 shadow-sm">
              {!selected ? (
                <p className="text-sm text-muted-foreground">Select a stage to configure</p>
              ) : (
                <div className="space-y-5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <GitBranch className="h-5 w-5 text-primary" />
                        <h2 className="text-xl font-bold">{selected.name}</h2>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {selected.description || "No description"}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1"
                        onClick={() => moveStage(selected.id, "up")}
                      >
                        <ArrowUp className="h-3.5 w-3.5" /> Up
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1"
                        onClick={() => moveStage(selected.id, "down")}
                      >
                        <ArrowDown className="h-3.5 w-3.5" /> Down
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1"
                        onClick={() => {
                          setEditingStage(selected);
                          setStageSheetOpen(true);
                        }}
                      >
                        <Pencil className="h-3.5 w-3.5" /> Edit
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1 text-destructive hover:text-destructive"
                        onClick={() =>
                          setDeleteTarget({ type: "stage", id: selected.id, name: selected.name })
                        }
                      >
                        <Trash2 className="h-3.5 w-3.5" /> Delete
                      </Button>
                    </div>
                  </div>

                  <div className="grid gap-3 sm:grid-cols-3">
                    <div className="rounded-lg border bg-muted/20 px-3 py-2">
                      <div className="text-[10px] uppercase text-muted-foreground">Type</div>
                      <div className="font-semibold">{STAGE_TYPE_LABEL[selected.stageType]}</div>
                    </div>
                    <div className="rounded-lg border bg-muted/20 px-3 py-2">
                      <div className="text-[10px] uppercase text-muted-foreground">SLA</div>
                      <div className="flex items-center gap-1 font-semibold">
                        <Clock3 className="h-3.5 w-3.5 text-muted-foreground" />
                        {selected.expectedDurationHours ?? "—"}h
                      </div>
                    </div>
                    <div className="rounded-lg border bg-muted/20 px-3 py-2">
                      <div className="text-[10px] uppercase text-muted-foreground">Warn / Critical</div>
                      <div className="font-semibold">
                        {selected.warningDurationHours ?? "—"}h / {selected.criticalDurationHours ?? "—"}h
                      </div>
                    </div>
                  </div>

                  <div>
                    <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      Statuses
                    </h3>
                    <div className="flex flex-wrap gap-2">
                      {selected.statuses.length ? (
                        selected.statuses.map((s) => (
                          <StatusPill
                            key={s.id}
                            label={s.trackName}
                            value={s.name}
                            showDot
                          />
                        ))
                      ) : (
                        <p className="text-sm text-muted-foreground">No statuses configured</p>
                      )}
                    </div>
                  </div>

                  {selected.stageType === 1 && (
                    <div>
                      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                        Parallel tracks
                      </h3>
                      <div className="overflow-hidden rounded-lg border">
                        <table className="w-full text-sm">
                          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
                            <tr>
                              <th className="px-3 py-2">Track</th>
                              <th className="px-3 py-2">Completion status</th>
                              <th className="px-3 py-2">Order</th>
                            </tr>
                          </thead>
                          <tbody>
                            {selected.parallelTracks.map((t) => (
                              <tr key={t.id} className="border-t">
                                <td className="px-3 py-2 font-medium">{t.trackName}</td>
                                <td className="px-3 py-2">
                                  <StatusPill value={t.completionStatus} />
                                </td>
                                <td className="px-3 py-2 tabular-nums">{t.sortOrder}</td>
                              </tr>
                            ))}
                            {!selected.parallelTracks.length && (
                              <tr>
                                <td colSpan={3} className="px-3 py-4 text-muted-foreground">
                                  No parallel tracks
                                </td>
                              </tr>
                            )}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}

                  <div>
                    <div className="mb-2 flex items-center justify-between">
                      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                        Related transitions
                      </h3>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 gap-1"
                        onClick={() => {
                          setEditingTransition(null);
                          setTransitionSheetOpen(true);
                        }}
                      >
                        <Plus className="h-3.5 w-3.5" /> Add transition
                      </Button>
                    </div>
                    <div className="space-y-2">
                      {relatedTransitions.map((t) => (
                        <div
                          key={t.id}
                          className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2"
                        >
                          <div className="min-w-0">
                            <div className="font-medium">{t.buttonLabel}</div>
                            <div className="text-xs text-muted-foreground">
                              {stageName(t.sourceStageId)} → {stageName(t.targetStageId)}
                              {t.conditions.rules[0]
                                ? ` · when ${t.conditions.rules[0].field}=${t.conditions.rules[0].value}`
                                : ""}
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            <FlagBadge tone={t.isActive ? "info" : "neutral"}>
                              {t.isActive ? "Active" : "Off"}
                            </FlagBadge>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7"
                              onClick={() => {
                                setEditingTransition(t);
                                setTransitionSheetOpen(true);
                              }}
                            >
                              Edit
                            </Button>
                          </div>
                        </div>
                      ))}
                      {!relatedTransitions.length && (
                        <p className="text-sm text-muted-foreground">No transitions for this stage</p>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </section>
          </div>
        </TabsContent>

        <TabsContent value="transitions" className="mt-4">
          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="font-semibold">Transition rules</h2>
                <p className="text-sm text-muted-foreground">
                  Action buttons shown on stage workbenches
                </p>
              </div>
              <Button
                size="sm"
                className="gap-1"
                onClick={() => {
                  setEditingTransition(null);
                  setTransitionSheetOpen(true);
                }}
              >
                <Plus className="h-3.5 w-3.5" /> Add transition
              </Button>
            </div>

            <div className="overflow-x-auto rounded-lg border">
              <table className="w-full min-w-[720px] text-sm">
                <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">Action</th>
                    <th className="px-3 py-2">From</th>
                    <th className="px-3 py-2">To</th>
                    <th className="px-3 py-2">Condition</th>
                    <th className="px-3 py-2">Roles</th>
                    <th className="px-3 py-2">Active</th>
                    <th className="px-3 py-2 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {[...def.transitionRules]
                    .sort((a, b) => a.sortOrder - b.sortOrder)
                    .map((t) => (
                      <tr key={t.id} className="border-t">
                        <td className="px-3 py-2.5 font-medium">{t.buttonLabel}</td>
                        <td className="px-3 py-2.5">{stageName(t.sourceStageId)}</td>
                        <td className="px-3 py-2.5">{stageName(t.targetStageId)}</td>
                        <td className="px-3 py-2.5 text-xs text-muted-foreground">
                          {t.conditions.rules[0]
                            ? `${t.conditions.rules[0].field} = ${t.conditions.rules[0].value}`
                            : "—"}
                        </td>
                        <td className="px-3 py-2.5 text-xs">{t.allowedRoles.join(", ") || "—"}</td>
                        <td className="px-3 py-2.5">
                          <Switch
                            checked={t.isActive}
                            onCheckedChange={() => {
                              toggleTransitionActive(t.id);
                              toast.success(t.isActive ? "Transition disabled" : "Transition enabled");
                            }}
                          />
                        </td>
                        <td className="px-3 py-2.5">
                          <div className="flex justify-end gap-1">
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7"
                              onClick={() => {
                                setEditingTransition(t);
                                setTransitionSheetOpen(true);
                              }}
                            >
                              <Pencil className="h-3.5 w-3.5" />
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 text-destructive"
                              onClick={() =>
                                setDeleteTarget({
                                  type: "transition",
                                  id: t.id,
                                  name: t.buttonLabel,
                                })
                              }
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </div>
        </TabsContent>

        <TabsContent value="settings" className="mt-4">
          <div className="max-w-xl rounded-xl border bg-card p-4 shadow-sm">
            <div className="mb-4 flex items-center gap-2">
              <Settings2 className="h-5 w-5 text-primary" />
              <h2 className="font-semibold">Pipeline settings</h2>
            </div>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="pipeline-name">Pipeline name</Label>
                <Input
                  id="pipeline-name"
                  value={metaName}
                  onChange={(e) => setMetaName(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="pipeline-desc">Description</Label>
                <Textarea
                  id="pipeline-desc"
                  rows={3}
                  value={metaDescription}
                  onChange={(e) => setMetaDescription(e.target.value)}
                />
              </div>
              <Button onClick={saveMeta}>Save settings</Button>
            </div>
          </div>
        </TabsContent>
      </Tabs>

      <StageFormSheet
        open={stageSheetOpen}
        onOpenChange={setStageSheetOpen}
        stage={editingStage}
        onSaved={refresh}
      />
      <TransitionFormSheet
        open={transitionSheetOpen}
        onOpenChange={setTransitionSheetOpen}
        stages={stages}
        transition={editingTransition}
        onSaved={refresh}
      />

      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Delete {deleteTarget?.type === "stage" ? "stage" : "transition"}?
            </AlertDialogTitle>
            <AlertDialogDescription>
              This removes <strong>{deleteTarget?.name}</strong>
              {deleteTarget?.type === "stage"
                ? " and any transitions connected to it."
                : " from the pipeline."}{" "}
              Demo changes stay in this browser session until reset.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete}>Delete</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
