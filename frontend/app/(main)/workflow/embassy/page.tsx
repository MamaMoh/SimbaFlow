"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { openFile, saveFile } from "@/lib/files/download";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { AgeCell } from "@/components/data-table/age-cell";
import { PendingCell } from "@/components/data-table/pending-cell";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { EmbassyRowActions } from "@/components/workflow/embassy-row-actions";
import { embassyApi, useEmbassyBoard, type EmbassyBoardRow } from "@/lib/api/embassy";
import { generateBulkVisaForms, visaFormGaps, type VisaFormGap } from "@/lib/api/candidates";
import { VisaDetailsDialog } from "@/components/workflow/visa-details-dialog";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Download, FileText, Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { selectionColumn } from "@/components/data-table/selection-column";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";

export default function EmbassyBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("embassy.read") || hasPermission("system.admin");
  // Ticking rows is only worth anything if something can be done with the selection, and the one
  // batch action — downloading their paperwork — is read under candidate.read. Reaching this board
  // is a different permission, so without it the column is dead weight in every row.
  const canSelect = hasPermission("candidate.read");
  const [search, setSearch] = useState("");
  const [rowSelection, setRowSelection] = useState<Record<string, boolean>>({});
  const [busy, setBusy] = useState<"tasheer" | "enjaze" | null>(null);
  // What the selected candidates are still missing. Non-empty means the dialog is open asking
  // for it; printing resumes on its own once it is saved.
  const [gaps, setGaps] = useState<VisaFormGap[]>([]);

  const { candidates, totalCount, isLoading, error, mutate, stageId } = useEmbassyBoard({
    search: search || undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<EmbassyBoardRow>[]>(
    () => [
      ...(canSelect ? [selectionColumn<EmbassyBoardRow>()] : []),
      indexColumn<EmbassyBoardRow>(),
      {
        accessorKey: "fullName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
      },
      {
        accessorKey: "partnerName",
        header: "Partner",
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        id: "medical",
        header: "Medical",
        cell: ({ row }) => {
          const v = row.original.statusValues?.medical;
          return <TrackChip value={v} warn={v === "Unfit"} />;
        },
      },
      {
        id: "tasheer",
        header: "Tasheer",
        cell: ({ row }) => {
          const v = row.original.statusValues?.tasheer;
          return <TrackChip value={v} warn={v === "Expired"} />;
        },
      },
      {
        id: "visa",
        header: "Visa",
        cell: ({ row }) => (
          <TrackChip value={row.original.statusValues?.visa} />
        ),
      },
      {
        accessorKey: "daysInStage",
        header: "In stage",
        cell: ({ getValue }) => <AgeCell days={getValue() as number} />,
      },
      {
        accessorKey: "daysSinceRegistered",
        header: "Case age",
        cell: ({ getValue }) => (
          <AgeCell days={getValue() as number} title="Days since the candidate was registered" />
        ),
      },
      {
        id: "tasheerAppointment",
        header: "Tasheer appt.",
        cell: ({ row }) => (
          <PendingCell
            value={row.original.statusValues?.tasheer_appointment_date}
            pendingLabel="No appointment"
          />
        ),
      },
      {
        id: "badges",
        header: "",
        cell: ({ row }) => {
          const s = row.original.statusValues ?? {};
          const mirrorLmis = s.medical === "Fit" && s.tasheer === "Book Done";
          return (
            <div className="flex flex-wrap gap-1">
              {s.medical === "Unfit" && (
                <Badge variant="destructive" className="text-xs">
                  Unfit
                </Badge>
              )}
              {s.tasheer === "Expired" && (
                <Badge variant="outline" className="text-xs text-amber-700">
                  Expired
                </Badge>
              )}
              {mirrorLmis && (
                <Badge variant="outline" className="text-xs">
                  Mirror→LMIS
                </Badge>
              )}
            </div>
          );
        },
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <EmbassyRowActions candidate={row.original} onMutate={() => mutate()} stageId={stageId} />
        ),
      },
    ],
    [mutate, canSelect]
  );

  const table = useReactTable({
    data: candidates,
    columns,
    state: { rowSelection },
    onRowSelectionChange: setRowSelection,
    getRowId: (row) => row.id,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  const selectedIds = Object.keys(rowSelection).filter((id) => rowSelection[id]);

  // Tasheer appointments are booked for a group, so the batch leaves as one sheet.
  const exportTasheer = async () => {
    if (selectedIds.length === 0) return toast.error("Select the candidates for this Tasheer batch");
    setBusy("tasheer");
    try {
      const blob = await embassyApi.exportTasheerList(selectedIds);
      saveFile(blob, `Tasheer list ${new Date().toISOString().slice(0, 10)}.xlsx`);
      toast.success(`${selectedIds.length} candidate${selectedIds.length === 1 ? "" : "s"} exported`);
      setRowSelection({});
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Export failed");
    } finally {
      setBusy(null);
    }
  };

  // One document, one enjaze per page — the run is printed as a batch.
  const buildEnjaze = async (ids: string[]) => {
    setBusy("enjaze");
    try {
      // Opened rather than saved: this batch exists to be printed in one pass, and nobody files it.
      openFile(await generateBulkVisaForms(ids));
      toast.success(`${ids.length} enjaze form${ids.length === 1 ? "" : "s"} ready to print`);
      setRowSelection({});
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not build the enjaze forms");
    } finally {
      setBusy(null);
    }
  };

  /**
   * Printing asks for what is missing instead of refusing because of it.
   *
   * It used to come back "Almaz Kebede and Hanan Ahmed have visa details still
   * missing" — which meant opening each candidate, finding the field, typing it, returning to
   * the board and re-ticking twenty rows. The refusal already knew what was wanted, so now it
   * is asked for here and the print carries on by itself.
   */
  const printEnjaze = async () => {
    if (selectedIds.length === 0) return toast.error("Select the candidates to print enjaze for");
    setBusy("enjaze");
    try {
      const found = (await visaFormGaps(selectedIds)).filter((g) => g.fields.length > 0);
      if (found.length > 0) {
        setBusy(null);
        setGaps(found);
        return;
      }
    } catch (e) {
      // The check is a convenience; if it cannot be made the print itself still refuses with the
      // same list, so there is no reason to stop here.
      toast.warning(e instanceof Error ? e.message : "Could not check the visa details");
    }
    await buildEnjaze(selectedIds);
  };

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="Embassy board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Embassy"
        count={totalCount}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search name or passport…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        }
      />

      <VisaDetailsDialog
        open={gaps.length > 0}
        gaps={gaps}
        onOpenChange={(next) => !next && setGaps([])}
        onFilled={() => {
          setGaps([]);
          mutate();
          void buildEnjaze(selectedIds);
        }}
      />

      {error && (
        <LoadError
          message={error instanceof Error ? error.message : String(error)}
          onRetry={() => mutate()}
        />
      )}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin mr-2" />
            Loading board…
          </div>
        ) : (
          <DataTable
            rowClickOpensActions
            exportFileName="embassy"
            table={table}
            toolbarEndActions={
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={busy !== null || selectedIds.length === 0}
                  onClick={() => void exportTasheer()}
                  className="gap-1.5"
                >
                  <Download className="h-3.5 w-3.5" />
                  {busy === "tasheer" ? "Exporting…" : `Tasheer list${selectedIds.length ? ` (${selectedIds.length})` : ""}`}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={busy !== null || selectedIds.length === 0}
                  onClick={() => void printEnjaze()}
                  className="gap-1.5"
                >
                  <FileText className="h-3.5 w-3.5" />
                  {busy === "enjaze" ? "Building…" : "Print enjaze"}
                </Button>
                <BulkDownloadButton
                  candidateIds={selectedIds}
                  onDownloaded={() => setRowSelection({})}
                />
              </div>
            }
            enableGlobalFilter={false}
            paginated
            emptyMessage="No candidates in Embassy yet — they appear here after “To Embassy” from New Contracts."
          />
        )}
      </div>
    </div>
  );
}
