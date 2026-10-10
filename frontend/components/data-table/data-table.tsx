"use client";
import * as React from "react";
import { flexRender } from "@tanstack/react-table";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { DataTablePagination } from "@/components/data-table/data-table-pagination";
import { DataTableToolbar } from "@/components/data-table/data-table-toolbar";
import { cn } from "@/lib/utils";
import { Input } from "@/components/ui/input";
import { Inbox, X } from "lucide-react";

/**
 * The actions column, pinned to the right edge.
 *
 * These tables are wider than the screen — the candidates list alone has fifteen columns — and
 * the one thing every row is for, its ⋯ menu, sat at the far right where it could only be
 * reached by scrolling sideways past everything else. Pinned, it is always under the cursor.
 *
 * An opaque background is not optional on a pinned cell: without it the columns underneath show
 * through as they scroll past.
 */
const PINNED =
  "sticky right-0 z-20 w-[60px] bg-background shadow-[-6px_0_6px_-6px_rgba(15,23,42,0.18)]";

/**
 * Opens an already-open menu where the pointer is, rather than over its trigger.
 *
 * Radix anchors a dropdown to the element that opened it, which for a row-body click is the ⋯
 * button pinned at the right edge — so clicking a name on the left threw the menu to the other
 * side of the screen, and on a wide table that is most of a metre away. There is no anchor
 * override on DropdownMenu, so the popper's own wrapper is moved instead, and kept there: Radix
 * recomputes the position on scroll and resize, which would otherwise snap it back to the
 * button mid-use.
 *
 * Clamped to the viewport, because a click near the right or bottom edge would otherwise open a
 * menu half off the screen.
 */
function openMenuAt(x: number, y: number): void {
  // The last one: a tooltip or an earlier menu may still be in the DOM, and the newest wrapper
  // is appended last.
  const wrappers = document.querySelectorAll<HTMLElement>("[data-radix-popper-content-wrapper]");
  const wrapper = wrappers[wrappers.length - 1];
  if (!wrapper) return;

  const place = () => {
    const { offsetWidth: w, offsetHeight: h } = wrapper;
    const left = Math.max(8, Math.min(x, window.innerWidth - w - 8));
    const top = Math.max(8, Math.min(y, window.innerHeight - h - 8));
    const wanted = `translate(${left}px, ${top}px)`;
    // Compared before writing, or the observer below would see its own write and loop forever.
    if (wrapper.style.transform === wanted) return;
    wrapper.style.setProperty("transform", wanted, "important");
  };

  place();

  const watcher = new MutationObserver(place);
  watcher.observe(wrapper, { attributeFilter: ["style"] });
  // Stops when the menu goes: Radix removes the wrapper from the DOM on close.
  const gone = new MutationObserver(() => {
    if (!wrapper.isConnected) {
      watcher.disconnect();
      gone.disconnect();
    }
  });
  gone.observe(document.body, { childList: true, subtree: true });
}

type PaginationProps = Omit<
  React.ComponentProps<typeof DataTablePagination>,
  "table"
>;

export interface DataTableProps<TData, TValue> {
  table: any;
  filterableColumns?: any[];
  searchableColumns?: any[];
  enableGlobalFilter?: boolean;
  newRowLink?: string;
  deleteRowsAction?: React.MouseEventHandler<HTMLButtonElement>;
  paginated?: boolean;
  viewHidden?: boolean;
  paginationProps?: PaginationProps;
  toolbarEndActions?: React.ReactNode;
  /** Filename stem for the toolbar's CSV export; omit to hide the button. */
  exportFileName?: string;
  /** Navigate/act when a row body is clicked (ignores clicks on buttons, links, inputs). */
  onRowClick?: (row: TData) => void;
  /**
   * The row itself is the target: right-click opens that row's ⋯ menu where the pointer is,
   * left-click ticks the row.
   *
   * Left-click used to open the menu, which made the table unusable in two ways at once. Every
   * click anywhere on a row threw a menu up, including the click meant to dismiss the last one
   * — so the menu appeared never to close. And ticking a row meant hitting the checkbox
   * exactly. Right-click is where a menu belongs, and it is what people try first.
   */
  rowClickOpensActions?: boolean;
  isFullscreen?: boolean;
  onToggleFullscreen?: () => void;
  onPrint?: (allData: any[], selectedRows: any[]) => void;
  searchPlaceholder?: string;
  useFilterPopover?: boolean;
  /**
   * A filter box under each column heading. On by default — every table in the app has one.
   * Set false where the rows are already narrowed by something else.
   */
  columnFilters?: boolean;
  /** Message shown inside the table body when there are no rows. */
  emptyMessage?: string;
  /** Full custom empty-state node (overrides emptyMessage). */
  emptyState?: React.ReactNode;
}

export function DataTable<TData, TValue>(
  props: DataTableProps<TData, TValue>
) {
  const {
    table,
    filterableColumns = [],
    searchableColumns = [],
    enableGlobalFilter = false,
    newRowLink,
    deleteRowsAction,
    paginated = true,
    viewHidden = false,
    paginationProps = {},
    toolbarEndActions,
  exportFileName,
  onRowClick,
  rowClickOpensActions = false,
    isFullscreen,
    onToggleFullscreen,
    onPrint,
    searchPlaceholder,
    useFilterPopover = false,
    columnFilters = true,
    emptyMessage = "No records to display yet.",
    emptyState,
  } = props;
  return (
    <div
      className={cn(
        "w-full space-y-2",
        isFullscreen ? "fixed inset-0 z-50 bg-background p-3 overflow-auto" : ""
      )}
    >
      {!viewHidden && (
        <DataTableToolbar
          exportFileName={exportFileName}
          table={table}
          filterableColumns={filterableColumns}
          searchableColumns={searchableColumns}
          enableGlobalFilter={enableGlobalFilter}
          newRowLink={newRowLink}
          deleteRowsAction={deleteRowsAction}
          toolbarEndActions={toolbarEndActions}
          onToggleFullscreen={onToggleFullscreen}
          isFullscreen={isFullscreen}
          onPrint={onPrint}
          searchPlaceholder={searchPlaceholder}
          useFilterPopover={useFilterPopover}
          tooltips={{
            delete: "Delete selected rows",
            new: "Add a new record",
            print: "Print table",
            fullscreen: isFullscreen ? "Exit fullscreen" : "Enter fullscreen",
            reset: "Reset filters",
          }}
        />
      )}
      <div className="w-full overflow-x-auto">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup: any) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header: any) => (
                  <TableHead
                    key={header.id}
                    className={cn(
                      "whitespace-nowrap",
                      header.column.id === "actions" && PINNED
                    )}
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(
                          header.column.columnDef.header,
                          header.getContext()
                        )}
                  </TableHead>
                ))}
              </TableRow>
            ))}

            {/* A box under each heading, filtering that column alone.
                One search box over the whole table answers "is this person here"; narrowing a
                list down to one partner, one stage and one occupation at once is a different
                question, and it was being answered by exporting to Excel. */}
            {columnFilters ? (
              <TableRow className="hover:bg-transparent">
                {table.getVisibleLeafColumns().map((column: any) => (
                  <th
                    key={column.id}
                    className={cn(
                      "bg-muted/30 p-1 align-middle",
                      column.id === "actions" && PINNED
                    )}
                  >
                    {column.getCanFilter() ? (
                      <div className="relative">
                        <Input
                          value={(column.getFilterValue() as string) ?? ""}
                          onChange={(e) =>
                            column.setFilterValue(e.target.value || undefined)
                          }
                          aria-label={`Filter ${column.id}`}
                          className="h-7 w-full min-w-[80px] bg-background px-2 pr-6 text-xs shadow-none"
                        />
                        {column.getFilterValue() ? (
                          <button
                            type="button"
                            aria-label={`Clear ${column.id} filter`}
                            onClick={() => column.setFilterValue(undefined)}
                            className="absolute right-1 top-1/2 -translate-y-1/2 rounded p-0.5 text-muted-foreground hover:text-foreground"
                          >
                            <X className="h-3 w-3" />
                          </button>
                        ) : null}
                      </div>
                    ) : null}
                  </th>
                ))}
              </TableRow>
            ) : null}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows?.length ? (
              table.getRowModel().rows.map((row: any) => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && "selected"}
                  className={onRowClick || rowClickOpensActions ? "cursor-pointer" : undefined}
                  onContextMenu={(e) => {
                    if (!rowClickOpensActions) return;
                    const trigger = e.currentTarget.querySelector<HTMLElement>(
                      '[aria-label="Row actions"]',
                    );
                    if (!trigger) return;
                    // No browser menu over ours.
                    e.preventDefault();
                    // Drive the row's own ⋯ trigger rather than duplicating each board's menu
                    // here — the menus differ per board and stay the single source of truth.
                    // The trigger opens on pointerdown, not click, so a plain .click() on it
                    // does nothing; dispatch what it actually listens for.
                    trigger.dispatchEvent(
                      new PointerEvent("pointerdown", {
                        bubbles: true,
                        cancelable: true,
                        button: 0,
                        pointerType: "mouse",
                      }),
                    );
                    // Then move it to the pointer. Radix has mounted the content by the next
                    // frame; before that there is nothing to move.
                    const { clientX, clientY } = e;
                    requestAnimationFrame(() => openMenuAt(clientX, clientY));
                  }}
                  onClick={(e) => {
                    // Don't hijack clicks on interactive controls inside the row.
                    if ((e.target as HTMLElement).closest(
                      'button, a, input, label, [role="checkbox"], [role="menuitem"], [data-no-row-click]'
                    )) return;
                    // Ticking the row is what a left click does on a table that has a
                    // selection, and a selection is what the Export and bulk buttons act on.
                    if (rowClickOpensActions && row.getCanSelect?.()) {
                      row.toggleSelected(!row.getIsSelected());
                      return;
                    }
                    onRowClick?.(row.original as TData);
                  }}
                >
                  {row.getVisibleCells().map((cell: any) => (
                    <TableCell
                      key={cell.id}
                      className={cn(cell.column.id === "actions" && PINNED)}
                    >
                      {flexRender(
                        cell.column.columnDef.cell,
                        cell.getContext()
                      )}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow className="hover:bg-transparent">
                <TableCell
                  colSpan={table.getAllColumns().length}
                  className="h-40 text-center align-middle"
                >
                  {emptyState ?? (
                    <div className="flex flex-col items-center justify-center gap-2 py-6 text-muted-foreground">
                      <Inbox className="h-8 w-8 opacity-40" />
                      <p className="text-sm">{emptyMessage}</p>
                    </div>
                  )}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
      {paginated && <DataTablePagination table={table} {...paginationProps} />}
    </div>
  );
}
