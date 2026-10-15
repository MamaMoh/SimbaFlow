"use client";

import { useState, useMemo, useCallback, useEffect } from "react";
import { useTableRows } from "@/lib/data/use-table-rows";
import useSWR from "swr";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type RowSelectionState,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { toQueryParam, type ColumnFilter } from "@/lib/data-table/column-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { toast } from "sonner";
import { generateCandidateCv } from "@/lib/api/candidates";
import { CandidateListActions } from "@/components/candidates/candidate-list-actions";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Loader2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";
import { saveFile } from "@/lib/files/download";
import { identityColumns } from "@/components/data-table/identity-columns";

interface CandidateRow {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId: string | null;
  currentStageName: string | null;
  countryOfTravel: string | null;
  partnerName: string | null;
  status: string;
  registeredAt: string;
  dateOfBirth?: string;
  age?: number | null;
  occupation?: string | null;
  sponsorName?: string | null;
  sponsorIdNumber?: string | null;
  visaNumber?: string | null;
  agentName?: string | null;
  worksIn?: string | null;
  phoneNumber?: string | null;
  contactPerson2?: string | null;
  contactPhone2?: string | null;
  experienceAbroadYears?: number | null;
  deletedBy?: string | null;
  deletedAt?: string | null;
  canDelete?: boolean;
}

const fetcher = (url: string) => fetch(url).then((res) => res.json());

export default function CandidatesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("candidate.read") || hasPermission("system.admin");
  // Registering someone is candidate.create on its own. Accepting candidate.update here put a
  // Create button in front of the field agent, who may correct a record but not open a new one,
  // and the form only said so after it had been filled in.
  const canCreate = hasPermission("candidate.create");
  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  // What the API was last asked for. The box updates on every keystroke; this follows a beat
  // later, so typing a name is one request rather than one per letter.
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"active" | "inactive" | "all">("active");
  // The stage chips above the table narrow the query rather than the rows the browser is
  // holding. "all" means the chip is off, and is never sent.
  const [stageFilter, setStageFilter] = useState<string>("all");
  // The filter row under the headings. Destination, partner, occupation and age used to be
  // controls in the toolbar; they are funnels on their own columns now, which is both fewer
  // places to look and every column rather than the four somebody picked in advance.
  const [columnFilters, setColumnFilters] = useState<ColumnFilter[]>([]);
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
  const [deleteTarget, setDeleteTarget] = useState<{
    id: string;
    name: string;
    stage: string;
  } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [generatingCvId, setGeneratingCvId] = useState<string | null>(null);

  useEffect(() => {
    // `?? ""` is load-bearing. An empty search box means "no filter" to TanStack, which it
    // signals as undefined — so this threw on every load of the page, and, worse, threw before
    // it could clear `search`: emptying the box left the list narrowed by whatever had been
    // typed last, with no way back short of a reload.
    const timer = setTimeout(() => setSearch((globalFilter ?? "").trim()), 300);
    return () => clearTimeout(timer);
  }, [globalFilter]);

  // Only the finished ones travel. A "greater than" with an empty box is the ordinary state
  // while somebody is still typing, and it should leave the list alone rather than empty it.
  const filterParam = useMemo(() => toQueryParam(columnFilters), [columnFilters]);

  /**
   * The list, narrowed by the database rather than by the browser.
   *
   * The search box used to filter the rows the table happened to be holding, which is the first
   * page of them — so a desk with two thousand people on the books searched a hundred and
   * concluded the candidate was not registered. Everything that narrows the list now goes to the
   * API, and the table shows what comes back.
   */
  const { data, error, isLoading, mutate } = useSWR(
    !permsLoading && canRead
      ? `/api/proxy/candidates?page=1&pageSize=200` +
        (search ? `&search=${encodeURIComponent(search)}` : "") +
        (statusFilter !== "active" ? `&status=${statusFilter}` : "") +
        (stageFilter !== "all" ? `&stageId=${encodeURIComponent(stageFilter)}` : "") +
        (filterParam ? `&filters=${encodeURIComponent(filterParam)}` : "")
      : null,
    fetcher,
    { revalidateOnFocus: false, keepPreviousData: true }
  );

  /**
   * What there is to filter by, counted by the database over every candidate in the current
   * bucket — not over the two hundred rows this page happens to be holding.
   *
   * The stage chips used to be built from the loaded rows, so an agency with more candidates
   * than one page saw counts that were a sample of themselves, and a stage whose people all
   * happened to fall past the page offered no chip at all.
   */
  const { data: filterData } = useSWR(
    !permsLoading && canRead
      ? `/api/proxy/candidates/filters` +
        (statusFilter !== "active" ? `?status=${statusFilter}` : "")
      : null,
    fetcher,
    { revalidateOnFocus: false, keepPreviousData: true }
  );

  const filters: {
    stages: { id: string | null; name: string; count: number }[];
    countries: { value: string; count: number }[];
    partners: { id: string; name: string; count: number }[];
    occupations: { value: string; count: number }[];
    total: number;
  } = filterData?.data ?? {
    stages: [],
    countries: [],
    partners: [],
    occupations: [],
    total: 0,
  };

  const candidates = useTableRows<CandidateRow>(data?.data?.items);
  const totalCount: number = data?.data?.totalCount ?? candidates.length;
  const loadFailed = !!error || (data && data.isSuccess === false);
  const selectedIds = useMemo(
    () => Object.keys(rowSelection).filter((id) => rowSelection[id]),
    [rowSelection]
  );



  const handleDelete = useCallback(async (id: string, name: string, stage: string) => {
    setDeleteTarget({ id, name, stage });
  }, []);

  const handleGenerateCv = useCallback(async (id: string) => {
    setGeneratingCvId(id);
    try {
      saveFile(await generateCandidateCv(id));
      toast.success("CV generated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "CV generation failed");
    } finally {
      setGeneratingCvId(null);
    }
  }, []);

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    const res = await fetch(`/api/proxy/candidates/${deleteTarget.id}`, { method: "DELETE" });
    const body = await res.json().catch(() => ({}));
    if (res.ok) {
      mutate();
      toast.success("Candidate deleted successfully");
    } else {
      toast.error(body?.error || "Failed to delete candidate");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  const columns: ColumnDef<CandidateRow>[] = useMemo(
    () => [
      {
        id: "select",
        header: ({ table }) => (
          <Checkbox
            checked={
              table.getIsAllPageRowsSelected() ||
              (table.getIsSomePageRowsSelected() && "indeterminate")
            }
            onCheckedChange={(value) => table.toggleAllPageRowsSelected(!!value)}
            aria-label="Select all"
          />
        ),
        cell: ({ row }) => (
          <Checkbox
            checked={row.getIsSelected()}
            onCheckedChange={(value) => row.toggleSelected(!!value)}
            aria-label="Select row"
          />
        ),
        size: 36,
        enableSorting: false,
      },
      {
        id: "index",
        header: "#",
        cell: ({ row }) => <span className="text-muted-foreground">{row.index + 1}</span>,
        size: 40,
        enableSorting: false,
      },
      {
        accessorKey: "fullName",
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      {
        accessorKey: "registeredAt",
        meta: { filter: { field: "registeredAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Date" />,
        cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
      },
      ...identityColumns<CandidateRow>(),
      {
        accessorKey: "age",
        meta: { filter: { field: "age", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Age" />,
        cell: ({ getValue }) => getValue() ?? "—",
        size: 50,
      },
      {
        accessorKey: "phoneNumber",
        meta: { filter: { field: "phoneNumber", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Phone" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        // The person to ring when the candidate herself cannot be reached, which is what the
        // desk means by "contact" — the column that used to carry that name was her own number.
        id: "contactPerson2",
        meta: { filter: { field: "contactPerson2", kind: "text" } },
        accessorFn: (row) =>
          [row.contactPerson2, row.contactPhone2].filter(Boolean).join(" · "),
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Contact person (2nd)" />
        ),
        cell: ({ row }) =>
          row.original.contactPerson2 || row.original.contactPhone2 ? (
            <div className="leading-tight">
              <div>{row.original.contactPerson2 || "—"}</div>
              {row.original.contactPhone2 ? (
                <div className="text-xs text-muted-foreground">
                  {row.original.contactPhone2}
                </div>
              ) : null}
            </div>
          ) : (
            "—"
          ),
      },
      {
        accessorKey: "occupation",
        meta: { filter: { field: "occupation", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Occupation" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "experienceAbroadYears",
        meta: { filter: { field: "experienceAbroadYears", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Experience" />,
        cell: ({ getValue }) => {
          const years = getValue() as number | null | undefined;
          if (years === null || years === undefined) return "—";
          return `${years} ${years === 1 ? "year" : "years"}`;
        },
        size: 90,
      },
      {
        accessorKey: "worksIn",
        meta: { filter: { field: "worksIn", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Worked in" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "sponsorName",
        meta: { filter: { field: "sponsorName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Sponsor" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "visaNumber",
        meta: { filter: { field: "visaNumber", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Visa No." />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "agentName",
        meta: { filter: { field: "agentName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Agent" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "partnerName",
        meta: { filter: { field: "partnerName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "currentStageName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Stage" />,
        cell: ({ getValue }) => {
          const stage = getValue() as string | null;
          return (
            <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700">
              {stage || "Intake"}
            </span>
          );
        },
      },
      {
        accessorKey: "status",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      },
      {
        // Only worth a column where there are removed records to account for, which is the
        // point of keeping them: a colleague who finds a candidate gone can see who took them
        // out and when, instead of wondering whether they were ever registered.
        id: "deletedBy",
        accessorFn: (row) => row.deletedBy ?? "",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Deleted by" />,
        cell: ({ row }) =>
          row.original.deletedBy || row.original.deletedAt ? (
            <div className="leading-tight">
              <div>{row.original.deletedBy || "—"}</div>
              {row.original.deletedAt ? (
                <div className="text-xs text-muted-foreground">
                  {new Date(row.original.deletedAt).toLocaleDateString()}
                </div>
              ) : null}
            </div>
          ) : (
            "—"
          ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => (
          <CandidateListActions
            candidateId={row.original.id}
            candidateName={row.original.fullName}
            stageName={row.original.currentStageName?.trim() || "Intake"}
            canDelete={row.original.canDelete !== false}
            status={row.original.status}
            isGeneratingCv={generatingCvId === row.original.id}
            onGenerateCv={handleGenerateCv}
            onDelete={handleDelete}
            onWorkflowChanged={() => mutate()}
          />
        ),
        size: 60,
        enableSorting: false,
      },
    ],
    [handleDelete, handleGenerateCv, generatingCvId, mutate]
  );

  // Memoised: this array is a dependency of the layout's own memos, and a fresh one every
  // render would rebuild the column order on every render.
  const columnIds = useMemo(
    () => columns.map((c: any) => c.id ?? c.accessorKey),
    [columns],
  );

  // The arrangement this person has made of this board, remembered on this computer.
  const layout = useTableLayout({
    table: "candidates",
    columnIds,
    managed: ["deletedBy"],
    defaultVisibility: {
      // These are filled in later stages, so on a fresh list every row shows "—" and the real
      // columns get pushed off the right edge. Available from the View menu when wanted.
      occupation: false,
      sponsorName: false,
      visaNumber: false,
      agentName: false,
      experienceAbroadYears: false,
      // Shown only where there is something to show — switching to Inactive turns it on.
      deletedBy: false,
    },
  });

  const table = useReactTable({
    data: candidates,
    columns,
    state: {
      sorting,
      globalFilter,
      rowSelection,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
    // The search box is the API's, and the per-column boxes are the browser's.
    //
    // manualFiltering used to turn both off together, because re-running the search over the
    // rows the API chose would hide ones it matched on a column nobody can see — somebody found
    // by E number has no E number column to match again. But it also silenced the column
    // filters. So client filtering is back on and the global filter is neutered instead: the
    // search string still travels to the API, and the column boxes narrow what comes back.
    globalFilterFn: () => true,
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    onRowSelectionChange: setRowSelection,
    getRowId: (row) => row.id,
    enableRowSelection: true,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: {
      pagination: { pageSize: 10 },
    },
  });

  // Who removed a record only means anything on the list that holds removed records. Switching
  // to Inactive brings the column in; switching back takes it out again, so the active list is
  // not carrying a column of dashes.
  useEffect(() => {
    table.getColumn("deletedBy")?.toggleVisibility(statusFilter !== "active");
  }, [statusFilter, table]);

  if (permsLoading) {
    return null;
  }

  if (!canRead) {
    return <AccessDenied resource="candidates" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Candidates" />

      {/*
        Who is where, without opening six boards to find out. Each board shows one stage; this is
        the whole pipeline in one row, and clicking a stage narrows the table below to it.
      */}
      {filters.stages.length > 0 && (
        <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
          <button
            type="button"
            onClick={() => setStageFilter("all")}
            className={
              "shrink-0 rounded-full border px-3 py-1 text-xs font-medium transition " +
              (stageFilter === "all"
                ? "border-green-800 bg-green-800 text-white"
                : "bg-background text-muted-foreground hover:text-foreground")
            }
          >
            All stages
            <span className="ml-1.5 opacity-70">{filters.total}</span>
          </button>
          {filters.stages.map((stage) => {
            // A candidate on no stage at all has no id to filter by; the chip is still worth
            // showing so the count adds up, it simply cannot be clicked.
            const id = stage.id;
            return (
              <button
                key={id ?? stage.name}
                type="button"
                disabled={!id}
                onClick={() => id && setStageFilter(id === stageFilter ? "all" : id)}
                className={
                  "shrink-0 rounded-full border px-3 py-1 text-xs font-medium transition " +
                  (stageFilter === id
                    ? "border-green-800 bg-green-800 text-white"
                    : "bg-background text-muted-foreground hover:text-foreground") +
                  (id ? "" : " cursor-default opacity-70")
                }
              >
                {stage.name}
                <span className="ml-1.5 opacity-70">{stage.count}</span>
              </button>
            );
          })}
        </div>
      )}

      {/* The table holds the first two hundred matches. Saying so is better than a list that
          quietly stops: the number below is what the filters actually matched. */}
      {totalCount > candidates.length ? (
        <p className="text-xs text-muted-foreground">
          Showing {candidates.length} of {totalCount} matches — narrow the filters or search to
          see the rest.
        </p>
      ) : null}

      {loadFailed && (
        <LoadError
          message={data?.error || (error instanceof Error ? error.message : undefined)}
          onRetry={() => mutate()}
        />
      )}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
            rowClickOpensActions
          onRowClick={(row: { id: string }) => router.push(`/candidates/${row.id}`)}
        exportFileName="candidates"
          table={table}
          layout={layout}
          filters={columnFilters}
          onFiltersChange={setColumnFilters}
          emptyMessage="No candidates yet — register a candidate to start the pipeline."
          enableGlobalFilter={true}
          searchPlaceholder="Search candidates..."
          paginated={true}
          toolbarEndActions={
            <div className="flex flex-wrap items-center gap-2">
              <div className="flex rounded-md border p-0.5" role="group" aria-label="Filter by status">
                {(["active", "inactive", "all"] as const).map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => setStatusFilter(s)}
                    className={
                      "rounded px-2.5 py-1 text-xs font-medium capitalize transition " +
                      (statusFilter === s
                        ? "bg-green-800 text-white"
                        : "text-muted-foreground hover:text-foreground")
                    }
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          }
          exportActions={
            <BulkDownloadButton
              candidateIds={selectedIds}
              onDownloaded={() => setRowSelection({})}
            />
          }
          createAction={
            canCreate ? (
              <Button
                size="sm"
                className="h-8 bg-green-800 text-white hover:bg-green-900"
                onClick={() => router.push("/candidates/new")}
              >
                <span className="mr-1">+</span> Create
              </Button>
            ) : null
          }
        />
      </div>

      {/*
        The warning says what the desk actually needs to hear, which is not "this cannot be
        undone" — it is that the candidate is standing in someone else's pipeline. Deleting from
        here used to pull a record out from under whoever was working it, with nothing asked and
        nobody told. The server now refuses past the first stage; this says so before the click.
      */}
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
        title="Delete candidate"
        description={
          `${deleteTarget?.name} is in ${deleteTarget?.stage}. Take them off the pipeline there ` +
          `before deleting — a colleague may be working this record, and removing it takes their ` +
          `work with it. Deleted candidates stay on the Inactive list under your name.`
        }
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
