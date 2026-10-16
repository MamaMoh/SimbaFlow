"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { AlertTriangle, Clock, Hourglass, Loader2 } from "lucide-react";
import type { FilterOption, WorkQueue, WorkQueueRow } from "@/lib/api/work-queues";
import { AgeCell } from "@/components/data-table/age-cell";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { NameCell } from "@/components/data-table/name-cell";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

/** The value the filters carry when they are not filtering. */
export const ANY = "__any";

/** What the funnels under these headings may be asked about, and how to read the answer. */
const FILTER_KINDS = {
  fullName: "text",
  passportNumber: "text",
  nextStep: "text",
  partnerName: "text",
  daysInStage: "number",
} as const;

/**
 * One half of a tab, as the same board the rest of the app uses.
 *
 * It was a list: name, step and partner stacked in a paragraph with the day count adrift at the
 * right edge. The same facts as columns can be read down — every name under every name — and
 * they arrive with what every other board has: the row number, the search box, the funnels under
 * the headings, the column menu and the export.
 *
 * The rows for a tab arrive whole, so the funnels are answered here rather than asked of the
 * API, exactly as on Roles, Agencies and Exchange rates.
 */
function QueueBoard({
  title,
  icon: Icon,
  rows,
  layoutKey,
  emptyMessage,
}: {
  title: string;
  icon: React.ElementType;
  rows: WorkQueueRow[];
  layoutKey: string;
  emptyMessage: string;
}) {
  const router = useRouter();
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();

  const narrowed = useMemo(
    () => applyColumnFilters(rows, filters, FILTER_KINDS),
    [rows, filters]
  );

  const columns = useMemo<ColumnDef<WorkQueueRow>[]>(
    () => [
      {
        id: "index",
        header: "#",
        // Muted on a span rather than on the cell: the ET theme blockifies a muted cell, which
        // drops it out of its own row.
        cell: ({ row }) => <span className="text-muted-foreground">{row.index + 1}</span>,
        size: 40,
        enableSorting: false,
      },
      {
        accessorKey: "fullName",
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        // To the board this person is on, at this person — not to their record. Whoever opens
        // My work is going there to do the step, and the record is one click further on.
        cell: ({ row }) => <NameCell href={row.original.href} name={row.original.fullName} />,
      },
      {
        accessorKey: "passportNumber",
        meta: { filter: { field: "passportNumber", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
        cell: ({ getValue }) => (
          <span className="whitespace-nowrap tabular-nums">{(getValue() as string) || "—"}</span>
        ),
      },
      {
        accessorKey: "nextStep",
        meta: { filter: { field: "nextStep", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Next step" />,
        cell: ({ row }) => (
          <div className="flex flex-wrap items-center gap-2">
            <span className={row.original.needsAction ? "font-medium" : undefined}>
              {row.original.nextStep}
            </span>
            {/* Something wrong outside this desk's step — an expired passport, an open case —
                that whoever picks the row up should know before they start on it. */}
            {row.original.warning && (
              <span className="inline-flex items-center gap-1 rounded-full bg-rose-500/15 px-2 py-0.5 text-[11px] font-medium text-rose-700 dark:text-rose-400">
                <AlertTriangle className="h-3 w-3" />
                {row.original.warning}
              </span>
            )}
          </div>
        ),
      },
      {
        accessorKey: "partnerName",
        meta: { filter: { field: "partnerName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ row }) =>
          row.original.partnerName || row.original.officeName ? (
            <div className="max-w-[200px]">
              <span className="block truncate">{row.original.partnerName || "—"}</span>
              {row.original.officeName && (
                <span className="block truncate text-xs text-muted-foreground">
                  {row.original.officeName}
                </span>
              )}
            </div>
          ) : (
            <span className="text-muted-foreground">—</span>
          ),
      },
      {
        accessorKey: "daysInStage",
        meta: { filter: { field: "daysInStage", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="In stage" />,
        cell: ({ row }) => (
          <AgeCell
            days={row.original.daysInStage}
            title={
              row.original.overdue
                ? `In this stage ${row.original.daysInStage} days — longer than 14`
                : `In this stage ${row.original.daysInStage} days`
            }
          />
        ),
      },
    ],
    []
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer. One key for both halves
  // of every tab: the columns are the same wherever you are, so arranging them once is enough.
  const layout = useTableLayout({ table: layoutKey, columnIds });

  const table = useReactTable({
    data: narrowed,
    columns,
    state: {
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getRowId: (row) => row.id,
    initialState: { pagination: { pageSize: 10 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <div className="mb-3 flex items-center gap-2">
        <Icon className="h-4 w-4 text-muted-foreground" />
        <h2 className="text-sm font-semibold">{title}</h2>
        <span className="text-xs tabular-nums text-muted-foreground">{rows.length}</span>
      </div>
      <DataTable
        layout={layout}
        table={table}
        // The row goes where the name goes. On every other board a row opens the thing it is
        // about, and a row here that only responded to its first cell would be the odd one.
        onRowClick={(row: WorkQueueRow) => router.push(row.href)}
        paginated
        enableGlobalFilter
        searchPlaceholder="Search name, passport or step…"
        filters={filters}
        onFiltersChange={setFilters}
        exportFileName={layoutKey}
        emptyMessage={emptyMessage}
      />
    </div>
  );
}

/**
 * The boards this role can open, as a strip of chips.
 *
 * Styled as the chip groups on Commissions and Partners, chosen one in the same green, so a tab
 * here reads as the same kind of switch a person already knows. A board with nothing to do shows
 * no number: ten grey zeros across the top are ten things to read past before reaching the one
 * count that means anything.
 */
function QueueTabs({
  queues,
  active,
  onSelect,
}: {
  queues: WorkQueue[];
  active: string;
  onSelect: (key: string) => void;
}) {
  return (
    <div role="tablist" aria-label="Boards you can open" className="flex flex-wrap gap-1">
      {queues.map((q) => {
        const selected = q.key === active;
        return (
          <button
            key={q.key}
            role="tab"
            aria-selected={selected}
            onClick={() => onSelect(q.key)}
            // The counts as well as the name: a tooltip alone says nothing to a screen reader,
            // and a title on its own would have the tab announce itself as "3 to do".
            aria-label={`${q.label}: ${q.needsActionCount} to do, ${q.waitingCount} waiting`}
            title={`${q.needsActionCount} to do, ${q.waitingCount} waiting`}
            className={cn(
              "inline-flex shrink-0 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition",
              selected
                ? "bg-green-800 text-white shadow-sm"
                : "text-muted-foreground hover:bg-muted hover:text-foreground"
            )}
          >
            {q.label}
            {q.needsActionCount > 0 && (
              <span
                className={cn(
                  "inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-semibold tabular-nums",
                  selected
                    ? "bg-white/20 text-white"
                    : q.overdueCount > 0
                      ? "bg-rose-500/15 text-rose-700 dark:text-rose-400"
                      : "bg-muted text-foreground"
                )}
              >
                {q.needsActionCount}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

/**
 * The order the tracks are offered in, which is the order the work happens in.
 *
 * Sorting them by name would put Visa before Tasheer on the Embassy board, and sorting by how
 * many rows each holds would move them about as the day went on. A track nobody named here is
 * still offered, after these, in the order it turned up.
 */
const TRACK_ORDER = [
  "Review",
  "Contract",
  "Medical",
  "Tasheer",
  "Visa",
  "Insurance",
  "Milestone",
  "Ticket",
  "Flight",
  "Notification",
  "Arrival",
  "Commission",
  "Dispute",
  "Fees",
  "Payment",
];

const ALL_TRACKS = "__all";

/**
 * The tracks on this tab, in order, with what each holds.
 *
 * Only the ones actually on the board: an Embassy board with no medicals left to book should
 * not offer a Medical switch that leads to an empty table.
 */
function tracksOf(rows: WorkQueueRow[]) {
  const seen = new Map<string, { toDo: number; waiting: number }>();
  for (const row of rows) {
    for (const track of row.tracks ?? []) {
      const tally = seen.get(track) ?? { toDo: 0, waiting: 0 };
      if (row.needsAction) tally.toDo += 1;
      else tally.waiting += 1;
      seen.set(track, tally);
    }
  }

  return [...seen.entries()]
    .sort((a, b) => {
      const ai = TRACK_ORDER.indexOf(a[0]);
      const bi = TRACK_ORDER.indexOf(b[0]);
      return (ai < 0 ? TRACK_ORDER.length : ai) - (bi < 0 ? TRACK_ORDER.length : bi);
    })
    .map(([name, tally]) => ({ name, ...tally }));
}

/**
 * Which part of this stage to look at: everything, or one track of it.
 *
 * Embassy runs medical, Tasheer and the visa side by side, and the person booking medicals this
 * morning does not want the Tasheer rows between them. One switch rather than three more tables
 * because the two tables below already answer the other question — whose turn it is — and a
 * stage split both ways at once is six lists to read.
 */
function TrackSwitch({
  tracks,
  active,
  onSelect,
}: {
  tracks: ReturnType<typeof tracksOf>;
  active: string;
  onSelect: (track: string) => void;
}) {
  const total = tracks.reduce((sum, t) => sum + t.toDo + t.waiting, 0);

  return (
    <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Part of this stage">
      <span className="mr-0.5 text-xs text-muted-foreground">Showing</span>
      {[{ name: ALL_TRACKS, label: "Everything", count: total }, ...tracks.map((t) => ({
        name: t.name,
        label: t.name,
        count: t.toDo + t.waiting,
        title: `${t.toDo} to do, ${t.waiting} waiting`,
      }))].map((chip) => {
        const selected = chip.name === active;
        return (
          <button
            key={chip.name}
            type="button"
            aria-pressed={selected}
            title={"title" in chip ? (chip.title as string) : undefined}
            onClick={() => onSelect(chip.name)}
            className={cn(
              "inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition",
              selected
                ? "border-green-800 bg-green-800/10 text-green-900 dark:text-green-300"
                : "border-transparent text-muted-foreground hover:bg-muted hover:text-foreground"
            )}
          >
            {chip.label}
            <span className="tabular-nums opacity-70">{chip.count}</span>
          </button>
        );
      })}
    </div>
  );
}

export type MyWorkBoardProps = {
  queues: WorkQueue[];
  active: WorkQueue;
  onSelectTab: (key: string) => void;
  partners: FilterOption[];
  partner: string;
  onPartner: (value: string) => void;
  offices: FilterOption[];
  office: string;
  onOffice: (value: string) => void;
  /** A reload behind an already-drawn board: the rows stay, a small spinner says why. */
  refreshing?: boolean;
};

/**
 * My work: the boards this role can open, and the two halves of whichever one is open.
 *
 * Two tables rather than one, because they are two different questions — what this desk owes,
 * and what it is waiting on somebody else for — and a single list sorted by one of them buries
 * the other. Each half is a board in its own right, with its own search, funnels and export.
 *
 * Partner and office sit above both: they narrow the tab rather than either half of it, and the
 * API answers them, so they belong beside the tabs and not in a toolbar.
 */
export function MyWorkBoard({
  queues,
  active,
  onSelectTab,
  partners,
  partner,
  onPartner,
  offices,
  office,
  onOffice,
  refreshing,
}: MyWorkBoardProps) {
  const [track, setTrack] = useState(ALL_TRACKS);

  const tracks = useMemo(() => tracksOf(active.items), [active]);
  // Held rather than reset when the tab changes: Medical means nothing on Tickets, and a switch
  // the new tab does not have is simply everything. Switching back to Embassy finds Medical
  // still chosen, which is where the person left it.
  const showing = tracks.some((t) => t.name === track) ? track : ALL_TRACKS;

  const rows = useMemo(
    () =>
      showing === ALL_TRACKS
        ? active.items
        : active.items.filter((r) => (r.tracks ?? []).includes(showing)),
    [active, showing]
  );

  const toDo = useMemo(() => rows.filter((r) => r.needsAction), [rows]);
  const waiting = useMemo(() => rows.filter((r) => !r.needsAction), [rows]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-card p-3 shadow-sm">
        {/* One board and there is nothing to switch between: the strip would be a single chip
            naming the page it is already on. */}
        {queues.length > 1 && (
          <QueueTabs queues={queues} active={active.key} onSelect={onSelectTab} />
        )}

        <div className="ml-auto flex flex-wrap items-center gap-2">
          {refreshing && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          {partners.length > 0 && (
            <Select value={partner} onValueChange={onPartner}>
              <SelectTrigger className="h-8 w-[150px]" aria-label="Partner">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>All partners</SelectItem>
                {partners.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          {offices.length > 0 && (
            <Select value={office} onValueChange={onOffice}>
              <SelectTrigger className="h-8 w-[140px]" aria-label="Office">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>All offices</SelectItem>
                {offices.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>

        {/* Only when there is more than one part to this stage: a single chip naming what the
            board already is would be a switch with nothing to switch to. */}
        {tracks.length > 1 && (
          <div className="w-full border-t pt-2">
            <TrackSwitch tracks={tracks} active={showing} onSelect={setTrack} />
          </div>
        )}
      </div>

      <QueueBoard
        title="Needs action"
        icon={Clock}
        rows={toDo}
        layoutKey="my-work-to-do"
        emptyMessage="Nothing waiting on you here."
      />
      <QueueBoard
        title="Waiting on others"
        icon={Hourglass}
        rows={waiting}
        layoutKey="my-work-waiting"
        emptyMessage="Nobody is waiting on someone else."
      />
    </div>
  );
}
