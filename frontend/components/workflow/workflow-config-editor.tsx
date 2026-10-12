"use client";

import { useMemo, useState } from "react";
import {
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  useReactTable,
  type ColumnDef,
} from "@tanstack/react-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DataTable } from "@/components/data-table/data-table";
import { indexColumn } from "@/components/data-table/index-column";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StageEditor } from "@/components/workflow/stage-editor";
import { TransitionSheet } from "@/components/workflow/transition-sheet";
import { MirrorRuleEditor } from "@/components/workflow/mirror-rule-editor";
import { useWorkflowDefinition } from "@/lib/api/workflow";
import { describeConditions } from "@/lib/workflow/condition-fields";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useTableRows } from "@/lib/data/use-table-rows";
import type {
  MirrorViewRule,
  WorkflowStage,
  WorkflowTransitionRule,
} from "@/types/workflow";
import {
  ArrowDown,
  ArrowUp,
  Loader2,
  MoreHorizontal,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";

const STAGE_TYPE_LABEL: Record<number, string> = {
  0: "Simple",
  1: "Parallel tracks",
  2: "Milestones",
};

/** The ⋯ menu every row in this app has, with whatever this row can do inside it. */
function RowMenu({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex justify-center">
      <DropdownMenu modal={false}>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="z-[200] w-56">
          {children}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export function WorkflowConfigEditor() {
  const { hasPermission, isAgencyOwner, isSuperAdmin, isLoading: permsLoading } =
    usePermissions();
  const { definition, stages: rawStages, isLoading, error, mutate } = useWorkflowDefinition();

  /**
   * Reading the pipeline and reshaping it are different things.
   *
   * A coordinator may need to see which step comes after which, and `workflow.configure` is
   * what opens this screen. Changing it moves everyone's work at once and cannot be undone from
   * a board, so it stays with the agency's owner and the platform team. The server refuses the
   * rest regardless; this is so nobody is offered a button that will only tell them no.
   */
  const canEdit = isAgencyOwner || isSuperAdmin;

  const stages = useTableRows<WorkflowStage>(rawStages);
  const transitions = useTableRows<WorkflowTransitionRule>(definition?.transitionRules);
  const mirrors = useTableRows<MirrorViewRule>(definition?.mirrorViewRules);

  const [stageEditorOpen, setStageEditorOpen] = useState(false);
  const [editingStage, setEditingStage] = useState<WorkflowStage | null>(null);
  const [transitionOpen, setTransitionOpen] = useState(false);
  const [editingTransition, setEditingTransition] = useState<WorkflowTransitionRule | null>(null);
  const [defaultSourceId, setDefaultSourceId] = useState<string | undefined>();
  const [mirrorOpen, setMirrorOpen] = useState(false);
  const [editingMirror, setEditingMirror] = useState<MirrorViewRule | null>(null);
  const [reordering, setReordering] = useState(false);

  /** Removals are confirmed in the app, not by a browser box with none of the page's styling. */
  const [pendingRemoval, setPendingRemoval] = useState<{
    title: string;
    description: string;
    run: () => Promise<void>;
  } | null>(null);
  const [removing, setRemoving] = useState(false);

  const stageNameById = useMemo(
    () => new Map(stages.map((s) => [s.id, s.name])),
    [stages],
  );

  const call = async (url: string, init: RequestInit, failure: string) => {
    const res = await fetch(url, init);
    const body = await res.json().catch(() => ({}));
    if (!res.ok || body?.isSuccess === false) throw new Error(body?.error || failure);
  };

  // ── reordering ────────────────────────────────────────────────────────────
  //
  // The whole order goes up at once rather than one stage's number: two steps sharing a number
  // leave their order to whatever the database returns, and that order is what the sidebar,
  // every stage picker and the candidate's progress bar all show.
  const moveStage = async (stage: WorkflowStage, by: -1 | 1) => {
    const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);
    const from = ordered.findIndex((s) => s.id === stage.id);
    const to = from + by;
    if (from < 0 || to < 0 || to >= ordered.length) return;

    const next = [...ordered];
    [next[from], next[to]] = [next[to], next[from]];

    setReordering(true);
    try {
      await call(
        "/api/proxy/workflow/config/stages/order",
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ stageIds: next.map((s) => s.id) }),
        },
        "Could not reorder the steps",
      );
      toast.success("Order saved");
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not reorder the steps");
    } finally {
      setReordering(false);
    }
  };

  const removeStage = (stage: WorkflowStage) =>
    setPendingRemoval({
      title: `Remove “${stage.name}”?`,
      description:
        "Its board disappears from the sidebar, along with every step into or out of it and " +
        "any mirroring onto it. Candidates standing in it must be moved first.",
      run: async () => {
        await call(
          `/api/proxy/workflow/config/stages/${stage.id}`,
          { method: "DELETE" },
          "Could not remove the step",
        );
        toast.success("Step removed");
        mutate();
      },
    });

  const removeTransition = (t: WorkflowTransitionRule) =>
    setPendingRemoval({
      title: `Remove “${t.buttonLabel}”?`,
      description: "Staff will no longer see this button on the board.",
      run: async () => {
        await call(
          `/api/proxy/workflow/config/transitions/${t.id}`,
          { method: "DELETE" },
          "Could not remove the move",
        );
        toast.success("Move removed");
        mutate();
      },
    });

  const removeMirror = (m: MirrorViewRule) =>
    setPendingRemoval({
      title: "Remove this mirroring?",
      description: `Candidates on ${m.sourceStageName} will stop appearing on ${m.targetStageName}.`,
      run: async () => {
        await call(
          `/api/proxy/workflow/config/mirrors/${m.id}`,
          { method: "DELETE" },
          "Could not remove the mirror",
        );
        toast.success("Mirroring removed");
        mutate();
      },
    });

  const confirmRemoval = async () => {
    if (!pendingRemoval) return;
    setRemoving(true);
    try {
      await pendingRemoval.run();
      setPendingRemoval(null);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not remove it");
    } finally {
      setRemoving(false);
    }
  };

  // ── columns ───────────────────────────────────────────────────────────────

  const ordered = useMemo(
    () => [...stages].sort((a, b) => a.sortOrder - b.sortOrder),
    [stages],
  );

  const stageColumns = useMemo<ColumnDef<WorkflowStage>[]>(() => {
    const cols: ColumnDef<WorkflowStage>[] = [
      indexColumn<WorkflowStage>(),
      {
        accessorKey: "name",
        header: "Step",
        cell: ({ row }) => (
          <div className="min-w-0">
            <span className="font-medium">{row.original.name}</span>
            {row.original.description && (
              <p className="truncate text-xs text-muted-foreground">
                {row.original.description}
              </p>
            )}
          </div>
        ),
      },
      {
        id: "stageType",
        header: "Kind",
        accessorFn: (s) => STAGE_TYPE_LABEL[s.stageType] ?? "—",
        cell: ({ getValue }) => <Badge variant="outline">{getValue() as string}</Badge>,
      },
      {
        id: "tracks",
        header: "Tracks",
        accessorFn: (s) =>
          (s.parallelTracks ?? []).map((t) => t.trackName).join(", ") || "—",
        cell: ({ getValue }) => (
          <span className="text-sm text-muted-foreground">{getValue() as string}</span>
        ),
      },
      {
        id: "statuses",
        header: "Statuses",
        accessorFn: (s) => s.statuses?.length ?? 0,
        cell: ({ getValue }) => (
          <span className="tabular-nums text-muted-foreground">{getValue() as number}</span>
        ),
      },
      {
        id: "position",
        header: "Position",
        enableColumnFilter: false,
        cell: ({ row }) => (
          <div className="flex flex-wrap items-center gap-1">
            <span className="tabular-nums text-muted-foreground">
              {row.original.sortOrder}
            </span>
            {row.original.isInitialStage && <Badge variant="secondary">First</Badge>}
            {row.original.isFinalStage && <Badge variant="secondary">Last</Badge>}
          </div>
        ),
      },
    ];

    if (canEdit) {
      cols.push({
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        enableColumnFilter: false,
        cell: ({ row }) => {
          const stage = row.original;
          const index = ordered.findIndex((s) => s.id === stage.id);
          return (
            <RowMenu>
              <DropdownMenuItem
                onClick={() => {
                  setEditingStage(stage);
                  setStageEditorOpen(true);
                }}
              >
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </DropdownMenuItem>
              <DropdownMenuItem
                onClick={() => {
                  setEditingTransition(null);
                  setDefaultSourceId(stage.id);
                  setTransitionOpen(true);
                }}
              >
                <Plus className="mr-2 h-4 w-4" /> Add a move from here
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                disabled={index <= 0 || reordering}
                onSelect={(e) => {
                  e.preventDefault();
                  void moveStage(stage, -1);
                }}
              >
                <ArrowUp className="mr-2 h-4 w-4" /> Move up
              </DropdownMenuItem>
              <DropdownMenuItem
                disabled={index < 0 || index >= ordered.length - 1 || reordering}
                onSelect={(e) => {
                  e.preventDefault();
                  void moveStage(stage, 1);
                }}
              >
                <ArrowDown className="mr-2 h-4 w-4" /> Move down
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                className="text-destructive focus:text-destructive"
                onClick={() => removeStage(stage)}
              >
                <Trash2 className="mr-2 h-4 w-4" /> Remove
              </DropdownMenuItem>
            </RowMenu>
          );
        },
      });
    }
    return cols;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canEdit, ordered, reordering]);

  const transitionColumns = useMemo<ColumnDef<WorkflowTransitionRule>[]>(() => {
    const cols: ColumnDef<WorkflowTransitionRule>[] = [
      indexColumn<WorkflowTransitionRule>(),
      { accessorKey: "buttonLabel", header: "Button" },
      {
        id: "from",
        header: "From",
        accessorFn: (t) => stageNameById.get(t.sourceStageId) ?? "—",
      },
      {
        id: "to",
        header: "To",
        accessorFn: (t) => stageNameById.get(t.targetStageId) ?? "—",
      },
      {
        id: "who",
        header: "Who can do it",
        accessorFn: (t) =>
          t.allowedRoles?.length ? t.allowedRoles.join(", ") : "Anyone with access",
        cell: ({ row }) =>
          row.original.allowedRoles?.length ? (
            <div className="flex flex-wrap gap-1">
              {row.original.allowedRoles.map((r) => (
                <StatusBadge
                  key={r}
                  tone="info"
                  label={r.replace(/([A-Z])/g, " $1").trim()}
                  withDot={false}
                />
              ))}
            </div>
          ) : (
            <span className="text-xs text-muted-foreground">Anyone with access</span>
          ),
      },
      {
        id: "conditions",
        header: "Offered when",
        accessorFn: (t) => describeConditions(t.conditions),
        cell: ({ getValue }) => (
          <span className="text-sm text-muted-foreground">{getValue() as string}</span>
        ),
      },
      {
        id: "requires",
        header: "Requires",
        accessorFn: (t) => (t.requiredFields?.length ? t.requiredFields.join(", ") : "—"),
        cell: ({ getValue }) => (
          <span className="text-sm text-muted-foreground">{getValue() as string}</span>
        ),
      },
      {
        id: "isActive",
        header: "Active",
        accessorFn: (t) => (t.isActive ? "Yes" : "No"),
        cell: ({ row }) => (
          <StatusBadge
            tone={row.original.isActive ? "success" : "neutral"}
            label={row.original.isActive ? "Yes" : "No"}
          />
        ),
      },
    ];

    if (canEdit) {
      cols.push({
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        enableColumnFilter: false,
        cell: ({ row }) => (
          <RowMenu>
            <DropdownMenuItem
              onClick={() => {
                setEditingTransition(row.original);
                setTransitionOpen(true);
              }}
            >
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => removeTransition(row.original)}
            >
              <Trash2 className="mr-2 h-4 w-4" /> Remove
            </DropdownMenuItem>
          </RowMenu>
        ),
      });
    }
    return cols;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canEdit, stageNameById]);

  const mirrorColumns = useMemo<ColumnDef<MirrorViewRule>[]>(() => {
    const cols: ColumnDef<MirrorViewRule>[] = [
      indexColumn<MirrorViewRule>(),
      { accessorKey: "sourceStageName", header: "From board" },
      { accessorKey: "targetStageName", header: "Also appears on" },
      {
        id: "conditions",
        header: "When",
        accessorFn: (m) => describeConditions(m.conditions),
        cell: ({ getValue }) => (
          <span className="text-sm text-muted-foreground">{getValue() as string}</span>
        ),
      },
      {
        id: "isActive",
        header: "Active",
        accessorFn: (m) => (m.isActive ? "Yes" : "No"),
        cell: ({ row }) => (
          <StatusBadge
            tone={row.original.isActive ? "success" : "neutral"}
            label={row.original.isActive ? "Yes" : "No"}
          />
        ),
      },
    ];

    if (canEdit) {
      cols.push({
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        enableColumnFilter: false,
        cell: ({ row }) => (
          <RowMenu>
            <DropdownMenuItem
              onClick={() => {
                setEditingMirror(row.original);
                setMirrorOpen(true);
              }}
            >
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => removeMirror(row.original)}
            >
              <Trash2 className="mr-2 h-4 w-4" /> Remove
            </DropdownMenuItem>
          </RowMenu>
        ),
      });
    }
    return cols;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canEdit]);

  const common = {
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  };

  const stageTable = useReactTable({ data: ordered, columns: stageColumns, ...common });
  const transitionTable = useReactTable({
    data: transitions,
    columns: transitionColumns,
    ...common,
  });
  const mirrorTable = useReactTable({ data: mirrors, columns: mirrorColumns, ...common });

  // ── render ────────────────────────────────────────────────────────────────

  if (permsLoading || isLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!hasPermission("workflow.configure") && !hasPermission("system.admin")) {
    return <AccessDenied resource="workflow setup" />;
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
        title="No workflow yet"
        description="This agency has no active pipeline. Provision or seed the default one first."
      />
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Workflow setup"
        description={
          canEdit
            ? "The order of the steps below is the order of the boards in the sidebar."
            : "Your agency’s pipeline. It is changed by the agency owner or the platform team."
        }
        actions={
          canEdit ? (
            <div className="flex flex-wrap gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  setEditingMirror(null);
                  setMirrorOpen(true);
                }}
              >
                <Plus className="mr-1 h-4 w-4" /> Mirroring
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  setEditingTransition(null);
                  setDefaultSourceId(undefined);
                  setTransitionOpen(true);
                }}
              >
                <Plus className="mr-1 h-4 w-4" /> Move
              </Button>
              <Button
                size="sm"
                className="bg-green-800 text-white hover:bg-green-900"
                onClick={() => {
                  setEditingStage(null);
                  setStageEditorOpen(true);
                }}
              >
                <Plus className="mr-1 h-4 w-4" /> Step
              </Button>
            </div>
          ) : null
        }
      />

      <Section
        title="Steps"
        count={ordered.length}
        hint="Each one is a board. Candidates stand in exactly one at a time."
      >
        <DataTable
          table={stageTable}
          paginated
          exportFileName="workflow-steps"
          emptyMessage="No steps yet. Add one to start building the pipeline."
        />
      </Section>

      <Section
        title="Moves between steps"
        count={transitions.length}
        hint="One button in a candidate's ⋯ menu. Who may press it, when it is offered, and what has to be filled in first."
      >
        <DataTable
          table={transitionTable}
          paginated
          exportFileName="workflow-moves"
          emptyMessage="No moves yet. Without them a candidate cannot leave the step they are in."
        />
      </Section>

      <Section
        title="Board mirroring"
        count={mirrors.length}
        hint="Shows a candidate on a second board without moving them off the first — how Embassy work reaches the LMIS desk before it is finished."
      >
        <DataTable
          table={mirrorTable}
          paginated
          exportFileName="workflow-mirroring"
          emptyMessage="No mirroring. Candidates appear only on the board matching their current step."
        />
      </Section>

      <StageEditor
        open={stageEditorOpen}
        onOpenChange={setStageEditorOpen}
        stage={editingStage}
        defaultSortOrder={ordered.length + 1}
        onSaved={() => mutate()}
      />
      <TransitionSheet
        open={transitionOpen}
        onOpenChange={setTransitionOpen}
        transition={editingTransition}
        stages={ordered}
        defaultSourceId={defaultSourceId}
        onSaved={() => mutate()}
      />
      <MirrorRuleEditor
        open={mirrorOpen}
        onOpenChange={setMirrorOpen}
        rule={editingMirror}
        stages={ordered}
        onSaved={() => mutate()}
      />
      <ConfirmDialog
        open={!!pendingRemoval}
        onOpenChange={(open) => !open && setPendingRemoval(null)}
        title={pendingRemoval?.title}
        description={pendingRemoval?.description}
        actionLabel="Remove"
        isPending={removing}
        onConfirm={() => void confirmRemoval()}
      />
    </div>
  );
}

/** A card with a heading, a count and one line saying what the thing in it is. */
function Section({
  title,
  count,
  hint,
  children,
}: {
  title: string;
  count: number;
  hint: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
      <div>
        <h2 className="flex items-baseline gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
          <span className="tabular-nums font-normal">{count}</span>
        </h2>
        <p className="mt-0.5 text-xs text-muted-foreground">{hint}</p>
      </div>
      {children}
    </section>
  );
}
