"use client";

import { useState, useMemo } from "react";
import useSWR from "swr";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Button } from "@/components/ui/button";
import { MoreHorizontal, Eye, Pencil, Trash2 } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CreateCandidateSheet } from "@/components/candidates/create-candidate-sheet";
import { EditCandidateSheet } from "@/components/candidates/edit-candidate-sheet";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { toast } from "sonner";

interface CandidateRow {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId: string | null;
  currentStageName: string | null;
  countryOfTravel: string | null;
  officeName: string | null;
  status: string;
  registeredAt: string;
}

const fetcher = (url: string) => fetch(url).then(res => res.json());

export default function CandidatesPage() {
  const { hasPermission } = usePermissions();
  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [editCandidate, setEditCandidate] = useState<CandidateRow | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, isLoading, mutate } = useSWR(
    `/api/proxy/candidates?page=1&pageSize=100${globalFilter ? `&search=${encodeURIComponent(globalFilter)}` : ""}`,
    fetcher,
    { revalidateOnFocus: false }
  );

  const candidates: CandidateRow[] = data?.data?.items || [];

  const handleDelete = async (id: string, name: string) => {
    setDeleteTarget({ id, name });
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    const res = await fetch(`/api/proxy/candidates/${deleteTarget.id}`, { method: "DELETE" });
    if (res.ok) {
      mutate();
      toast.success("Candidate deleted successfully");
    } else {
      toast.error("Failed to delete candidate");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  const columns: ColumnDef<CandidateRow>[] = useMemo(() => [
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
        <Link href={`/candidates/${row.original.id}`} className="font-medium text-foreground hover:underline">
          {row.original.fullName}
        </Link>
      ),
    },
    {
      accessorKey: "passportNumber",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
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
      accessorKey: "countryOfTravel",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Country" />,
      cell: ({ getValue }) => getValue() || "—",
    },
    {
      accessorKey: "registeredAt",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Registered" />,
      cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="h-8 w-8" data-testid={`candidate-actions-${row.original.id}`}>
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => router.push(`/candidates/${row.original.id}`)}>
              <Eye className="h-4 w-4 mr-2" /> View Details
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => setEditCandidate(row.original)}>
              <Pencil className="h-4 w-4 mr-2" /> Edit
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => handleDelete(row.original.id, row.original.fullName)} className="text-destructive">
              <Trash2 className="h-4 w-4 mr-2" /> Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      size: 60,
      enableSorting: false,
    },
  ], [router, handleDelete]);

  const table = useReactTable({
    data: candidates,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
  });

  return (
    <div className="flex flex-col gap-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Candidates</h1>
        <p className="text-sm text-muted-foreground">Manage candidate registrations and track their pipeline progress</p>
      </div>

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search candidates..."
          paginated={true}
          toolbarEndActions={
            <Button size="sm" className="h-8 bg-green-800 hover:bg-green-900 text-white" onClick={() => setCreateOpen(true)}>
              <span className="mr-1">+</span> Create
            </Button>
          }
        />
      </div>

      <CreateCandidateSheet open={createOpen} onOpenChange={setCreateOpen} />
      <EditCandidateSheet
        candidate={editCandidate}
        open={!!editCandidate}
        onOpenChange={(open) => { if (!open) setEditCandidate(null); }}
        onUpdated={() => mutate()}
      />
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete candidate"
        description={`Are you sure you want to delete '${deleteTarget?.name}'? This action cannot be undone.`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
