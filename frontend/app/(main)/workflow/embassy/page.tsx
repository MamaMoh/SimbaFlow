"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { openFile, saveFile } from "@/lib/files/download";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { AgeCell } from "@/components/data-table/age-cell";
import { AppointmentCell } from "@/components/data-table/appointment-cell";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { EmbassyRowActions } from "@/components/workflow/embassy-row-actions";
import { embassyApi, useEmbassyBoard, type EmbassyBoardRow } from "@/lib/api/embassy";
import { generateBulkVisaForms, visaFormGaps } from "@/lib/api/candidates";
import { useVisaDetails } from "@/components/workflow/visa-details-host";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Download, FileText, Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { selectionColumn } from "@/components/data-table/selection-column";
import { BulkActionsMenu } from "@/components/workflow/bulk-actions-menu";
import { EMBASSY_BULK_ACTIONS } from "@/components/workflow/board-bulk-actions";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";

export default function EmbassyBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("embassy.read") || hasPermission("system.admin");
  // Ticking rows is only worth anything if something can be done with the selection: downloading
  // their paperwork, read under candidate.read, or the Actions menu, under embassy.update. Reaching
  // this board is a different permission, so without either the column is dead weight in every row.
  const canBulk = hasPermission("embassy.update") || hasPermission("system.admin");
  const canSelect = hasPermission("candidate.read") || canBulk;
  // The search box in the toolbar and the funnels under the headings. Both are answered by
  // the API: narrowing the page the browser happens to be holding would answer a different
  // question from the one being asked.
  const { globalFilter, setGlobalFilter, search, filters, setFilters, filterParam } =
    useBoardQuery();
  const [rowSelection, setRowSelection] = useState<Record<string, boolean>>({});
  const [busy, setBusy] = useState<"tasheer" | "enjaze" | null>(null);
  // The form that asks for missing details is mounted above every page; this is how a board
  // asks for it, with what to do once it has been filled in.
  const askForVisaDetails = useVisaDetails();

  const { candidates, totalCount, isLoading, error, mutate, stageId } = useEmbassyBoard({
    search: search || undefined,
    filters: filterParam,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<EmbassyBoardRow>[]>(
    () => [
      ...(canSelect ? [selectionColumn<EmbassyBoardRow>()] : []),
      indexColumn<EmbassyBoardRow>(),
      {
        accessorKey: "fullName",
        // The field name the server's whitelist knows this column by. The status tracks and
        // the day counts have none: the first live in a document on the candidate and the rest
        // are worked out per row, and a funnel that quietly matched nothing would be worse
        // than no funnel at all.
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      ...identityColumns<EmbassyBoardRow>(),
      {
        accessorKey: "partnerName",
        meta: { filter: { field: "partnerName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
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
        id: "tasheerAppointment",
        header: "Tasheer appt.",
        // Ahead of the two age chips: the appointment is the date on this board that anyone
        // can still do something about, and it was sitting past them at the right-hand edge.
        cell: ({ row }) => (
          <AppointmentCell
            date={row.original.statusValues?.tasheer_appointment_date}
            pendingLabel="No appointment"
            stillWaiting={row.original.statusValues?.tasheer === "Booked"}
          />
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

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "embassy", columnIds });

  const table = useReactTable({
    data: candidates,
    columns,
    state: {
      globalFilter,
      rowSelection,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The search box belongs to the API. Left as TanStack's own it would narrow the rows the
    // API already chose, hiding the ones it matched on a column that is not on screen.
    globalFilterFn: () => true,
    onGlobalFilterChange: setGlobalFilter,
    onRowSelectionChange: setRowSelection,
    getRowId: (row) => row.id,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  const selectedIds = Object.keys(rowSelection).filter((id) => rowSelection[id]);

  // Tasheer appointments are booked for a group, so the batch leaves as one sheet.
  const buildTasheer = async (ids: string[]) => {
    setBusy("tasheer");
    try {
      const blob = await embassyApi.exportTasheerList(ids);
      saveFile(blob, `Tasheer list ${new Date().toISOString().slice(0, 10)}.xlsx`);
      toast.success(`${ids.length} candidate${ids.length === 1 ? "" : "s"} exported`);
      setRowSelection({});
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Export failed");
    } finally {
      setBusy(null);
    }
  };

  /**
   * The sheet's first column is the E number, and it was going out blank.
   *
   * The Tasheer system keys on it, so a batch exported before the number was recorded is a
   * sheet that has to be done again. Asked for here the same way the enjaze asks — and only
   * the E number, because nothing else on that form stops this sheet being usable.
   */
  const exportTasheer = async () => {
    if (selectedIds.length === 0) return toast.error("Select the candidates for this Tasheer batch");
    setBusy("tasheer");
    try {
      const found = (await visaFormGaps(selectedIds))
        .map((g) => ({ ...g, fields: g.fields.filter((f) => f.key === "eNumber") }))
        .filter((g) => g.fields.length > 0);
      if (found.length > 0) {
        setBusy(null);
        // The same form, a shorter list — only the E numbers — and a different thing waiting on
        // the other side of it.
        askForVisaDetails({
          gaps: found,
          onFilled: () => {
            mutate();
            void buildTasheer(selectedIds);
          },
          onCancelled: () => mutate(),
        });
        return;
      }
    } catch (e) {
      // A convenience: the export still runs, with whatever is on file.
      toast.warning(e instanceof Error ? e.message : "Could not check the E numbers");
    }
    await buildTasheer(selectedIds);
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
        askForVisaDetails({
          gaps: found,
          onFilled: () => {
            mutate();
            void buildEnjaze(selectedIds);
          },
          // Saved without printing: the typing is on the record, so the board reloads to show
          // it, but no batch is built.
          onCancelled: () => mutate(),
        });
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
            layout={layout}
            rowClickOpensActions
            exportFileName="embassy"
            table={table}
            exportActions={
              <>
                {/* The Tasheer sheet and the enjaze pack are both ways of taking this board
                    somewhere else, so they sit with the CSV rather than beside it. */}
                <DropdownMenuItem
                  disabled={busy !== null || selectedIds.length === 0}
                  onSelect={(e) => {
                    e.preventDefault();
                    void exportTasheer();
                  }}
                >
                  <Download className="mr-2 h-4 w-4" />
                  {busy === "tasheer"
                    ? "Exporting…"
                    : `Tasheer list${selectedIds.length ? ` (${selectedIds.length})` : ""}`}
                </DropdownMenuItem>
                <DropdownMenuItem
                  disabled={busy !== null || selectedIds.length === 0}
                  onSelect={(e) => {
                    e.preventDefault();
                    void printEnjaze();
                  }}
                >
                  <FileText className="mr-2 h-4 w-4" />
                  {busy === "enjaze" ? "Building…" : "Print enjaze"}
                </DropdownMenuItem>
                <BulkDownloadButton
                  candidateIds={selectedIds}
                  onDownloaded={() => setRowSelection({})}
                />
              </>
            }
            toolbarEndActions={
              // Beside Export: the row menu's medical and Tasheer steps for every ticked row.
              canBulk ? (
                <BulkActionsMenu
                  selected={candidates.filter((c) => rowSelection[c.id])}
                  actions={EMBASSY_BULK_ACTIONS}
                  onClear={() => setRowSelection({})}
                  onDone={(done) => {
                    setRowSelection((s) => {
                      const next = { ...s };
                      for (const id of done) delete next[id];
                      return next;
                    });
                    void mutate();
                  }}
                />
              ) : undefined
            }
            enableGlobalFilter
            searchPlaceholder="Search name, passport or labour ID…"
            filters={filters}
            onFiltersChange={setFilters}
            paginated
            emptyMessage="No candidates in Embassy yet — they appear here after “To Embassy” from New Contracts."
          />
        )}
      </div>
    </div>
  );
}
