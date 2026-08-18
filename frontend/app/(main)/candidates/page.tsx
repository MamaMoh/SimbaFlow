"use client";

import { useState, useMemo, useCallback } from "react";
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
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { toast } from "sonner";
import { generateBulkCandidateCvs, generateCandidateCv } from "@/lib/api/candidates";
import { CandidateListActions } from "@/components/candidates/candidate-list-actions";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Files, Loader2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";

function openPdfInNewTab(blob: Blob) {
  const pdfBlob =
    blob.type === "application/pdf" ? blob : new Blob([blob], { type: "application/pdf" });
  const url = URL.createObjectURL(pdfBlob);
  const a = document.createElement("a");
  a.href = url;
  a.target = "_blank";
  a.rel = "noopener noreferrer";
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 120_000);
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

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
}

const fetcher = (url: string) => fetch(url).then((res) => res.json());

export default function CandidatesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("candidate.read") || hasPermission("system.admin");
  const canWrite =
    hasPermission("candidate.write") || hasPermission("system.admin");
  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [generatingCvId, setGeneratingCvId] = useState<string | null>(null);
  const [bulkGenerating, setBulkGenerating] = useState(false);

  const { data, error, isLoading, mutate } = useSWR(
    !permsLoading && canRead
      ? `/api/proxy/candidates?page=1&pageSize=100${globalFilter ? `&search=${encodeURIComponent(globalFilter)}` : ""}`
      : null,
    fetcher,
    { revalidateOnFocus: false }
  );

  const candidates: CandidateRow[] = data?.data?.items || [];
  const loadFailed = !!error || (data && data.isSuccess === false);
  const selectedIds = useMemo(
    () => Object.keys(rowSelection).filter((id) => rowSelection[id]),
    [rowSelection]
  );

  const handleDelete = useCallback(async (id: string, name: string) => {
    setDeleteTarget({ id, name });
  }, []);

  const handleGenerateCv = useCallback(async (id: string) => {
    setGeneratingCvId(id);
    try {
      const blob = await generateCandidateCv(id);
      openPdfInNewTab(blob);
      toast.success("CV generated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "CV generation failed");
    } finally {
      setGeneratingCvId(null);
    }
  }, []);

  const handleBulkGenerateCvs = async () => {
    if (selectedIds.length === 0) {
      toast.error("Select at least one candidate");
      return;
    }
    if (selectedIds.length > 50) {
      toast.error("Select at most 50 candidates at once");
      return;
    }
    setBulkGenerating(true);
    try {
      const blob = await generateBulkCandidateCvs(selectedIds);
      downloadBlob(blob, `cvs_${new Date().toISOString().slice(0, 10)}.zip`);
      toast.success(`Generated ${selectedIds.length} CV${selectedIds.length === 1 ? "" : "s"}`);
      setRowSelection({});
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Bulk CV generation failed");
    } finally {
      setBulkGenerating(false);
    }
  };

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
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      {
        accessorKey: "registeredAt",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Date" />,
        cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
      },
      {
        accessorKey: "age",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Age" />,
        cell: ({ getValue }) => getValue() ?? "—",
        size: 50,
      },
      {
        accessorKey: "occupation",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Occupation" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "worksIn",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Works In" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "sponsorName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Sponsor" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "visaNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Visa No." />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "agentName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Agent" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "partnerName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "labourId",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Labour ID" />,
        cell: ({ getValue }) => getValue() || "—",
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
        id: "actions",
        header: "Actions",
        cell: ({ row }) => (
          <CandidateListActions
            candidateId={row.original.id}
            candidateName={row.original.fullName}
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

  const table = useReactTable({
    data: candidates,
    columns,
    state: { sorting, globalFilter, rowSelection },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    onRowSelectionChange: setRowSelection,
    getRowId: (row) => row.id,
    enableRowSelection: true,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
  });

  if (permsLoading) {
    return null;
  }

  if (!canRead) {
    return <AccessDenied resource="candidates" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Candidates"
        description="Manage candidate registrations and track their pipeline progress"
      />

      {loadFailed && (
        <LoadError
          message={data?.error || (error instanceof Error ? error.message : undefined)}
          onRetry={() => mutate()}
        />
      )}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
        exportFileName="candidates"
          table={table}
          emptyMessage="No candidates yet — register a candidate to start the pipeline."
          enableGlobalFilter={true}
          searchPlaceholder="Search candidates..."
          paginated={true}
          toolbarEndActions={
            <div className="flex items-center gap-2">
              {selectedIds.length > 0 ? (
                <Button
                  size="sm"
                  variant="outline"
                  className="h-8 gap-1.5"
                  disabled={bulkGenerating}
                  onClick={handleBulkGenerateCvs}
                >
                  {bulkGenerating ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Files className="h-3.5 w-3.5" />
                  )}
                  Generate CVs ({selectedIds.length})
                </Button>
              ) : null}
              {canWrite ? (
                <Button
                  size="sm"
                  className="h-8 bg-green-800 hover:bg-green-900 text-white"
                  onClick={() => router.push("/candidates/new")}
                >
                  <span className="mr-1">+</span> Create
                </Button>
              ) : null}
            </div>
          }
        />
      </div>

      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
        title="Delete candidate"
        description={`Are you sure you want to delete '${deleteTarget?.name}'? This action cannot be undone.`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
