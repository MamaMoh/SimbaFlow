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
import type { TableLayout } from "@/lib/data-table/use-table-layout";
import {
  filterFor,
  withFilter,
  type ColumnFilter,
  type ColumnFilterSpec,
} from "@/lib/data-table/column-filters";
import { ColumnFilterCell } from "@/components/data-table/column-filter-cell";
import { headingOf } from "@/components/data-table/column-heading";

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
let unplace: (() => void) | null = null;

function openMenuAt(x: number, y: number): void {
  // Only ever one menu, and only ever one of these running: a second right-click cancels the
  // first rather than leaving it to keep writing.
  unplace?.();

  // The last wrapper: a tooltip or an earlier menu may still be in the DOM, and the newest is
  // appended last.
  const wrappers = document.querySelectorAll<HTMLElement>("[data-radix-popper-content-wrapper]");
  const wrapper = wrappers[wrappers.length - 1];
  if (!wrapper) return;

  const place = () => {
    const { offsetWidth: w, offsetHeight: h } = wrapper;
    const left = Math.max(8, Math.min(x, window.innerWidth - w - 8));
    const top = Math.max(8, Math.min(y, window.innerHeight - h - 8));
    const wanted = `translate(${left}px, ${top}px)`;
    if (wrapper.style.transform !== wanted)
      wrapper.style.setProperty("transform", wanted, "important");
  };

  // Held for a few frames rather than watched for the life of the menu.
  //
  // This used to be a MutationObserver on the wrapper's style, plus a second one on the whole
  // of document.body to know when the menu went away. Two problems with that. The wide one
  // fired on every DOM change anywhere on the page for as long as a menu was open, which on a
  // table that re-renders is thousands of callbacks. And the narrow one re-applied our
  // position every time the popper wrote its own — while the popper, watching the element it
  // had just been moved out from under, recomputed and wrote again. Two pieces of code each
  // correcting the other is a loop with no exit, and the tab stops answering.
  //
  // Radix settles its position within a frame or two of mounting, so a short window covers it.
  // Nothing of ours runs after that, which is the point.
  let frame = 0;
  let raf = 0;
  const tick = () => {
    if (!wrapper.isConnected || frame++ > 12) {
      unplace = null;
      return;
    }
    place();
    raf = requestAnimationFrame(tick);
  };
  place();
  raf = requestAnimationFrame(tick);

  unplace = () => {
    cancelAnimationFrame(raf);
    unplace = null;
  };
}

/**
 * Whether the event happened on the row itself, rather than inside something the row rendered
 * into a portal.
 *
 * Every row carries its own dialogs and sheets, and React sends their events back up the tree
 * they were declared in — not the tree they are drawn in. So a right-click inside an open form
 * arrived here as a right-click on the row underneath it: the row opened its ⋯ menu over the
 * form, and the form, seeing a menu open outside itself, closed. Typing in a form ticked the
 * row's checkbox. The row's own handlers only have business with the row's own cells.
 */
function inThisRow(event: React.MouseEvent<HTMLElement>): boolean {
  return event.currentTarget.contains(event.target as Node);
}

type PaginationProps = Omit<
  React.ComponentProps<typeof DataTablePagination>,
  "table"
>;

export interface DataTableProps<TData, TValue> {
  table: any;
  enableGlobalFilter?: boolean;
  newRowLink?: string;
  deleteRowsAction?: React.MouseEventHandler<HTMLButtonElement>;
  paginated?: boolean;
  viewHidden?: boolean;
  paginationProps?: PaginationProps;
  toolbarEndActions?: React.ReactNode;
  /** Filename stem for the toolbar's CSV export; omit to leave CSV out of the Export menu. */
  exportFileName?: string;
  /**
   * Extra items under Export — the paperwork download, where a board has one.
   *
   * `DropdownMenuItem`s, supplied by the board, because what else there is to take off a board
   * differs per board and the toolbar has no business knowing.
   */
  exportActions?: React.ReactNode;
  /** The board's Create button, drawn last in the toolbar. */
  createAction?: React.ReactNode;
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
  searchPlaceholder?: string;
  /**
   * A filter box under each column heading. On by default — every table in the app has one.
   * Set false where the rows are already narrowed by something else.
   */
  columnFilters?: boolean;
  /**
   * The filter row's current state, when this board filters in the database.
   *
   * Supplied together: without `onFiltersChange` there is nothing to do with a change, so the
   * funnels are not drawn at all and the row falls back to the boxes that narrow what is
   * already on screen.
   */
  filters?: ColumnFilter[];
  onFiltersChange?: (next: ColumnFilter[]) => void;
  /** Message shown inside the table body when there are no rows. */
  emptyMessage?: string;
  /** Full custom empty-state node (overrides emptyMessage). */
  emptyState?: React.ReactNode;
  /**
   * Lets a heading be dragged onto another heading's place.
   *
   * Passed the layout this board remembers. Without it the headings are fixed, which is what
   * every table did before and what a board with nothing worth rearranging still wants.
   */
  layout?: TableLayout;
}

export function DataTable<TData, TValue>(
  props: DataTableProps<TData, TValue>
) {
  const {
    table,
    enableGlobalFilter = false,
    newRowLink,
    deleteRowsAction,
    paginated = true,
    viewHidden = false,
    paginationProps = {},
    toolbarEndActions,
  exportFileName,
  exportActions,
  createAction,
  onRowClick,
  rowClickOpensActions = false,
    isFullscreen,
    searchPlaceholder,
    columnFilters = true,
    emptyMessage = "No records to display yet.",
    emptyState,
    layout,
    filters,
    onFiltersChange,
  } = props;

  const serverFiltered = !!filters && !!onFiltersChange;

  // Which heading is under the pointer mid-drag, so the gap opens where it will land. Held
  // here rather than in the layout: it is about this drag, not about what gets remembered.
  const [dragging, setDragging] = React.useState<string | null>(null);
  const [over, setOver] = React.useState<string | null>(null);
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
          exportActions={exportActions}
          createAction={createAction}
          table={table}
          layout={layout}
          enableGlobalFilter={enableGlobalFilter}
          newRowLink={newRowLink}
          deleteRowsAction={deleteRowsAction}
          toolbarEndActions={toolbarEndActions}
          searchPlaceholder={searchPlaceholder}
          tooltips={{
            delete: "Delete selected rows",
            new: "Add a new record",
          }}
        />
      )}
      <div className="w-full overflow-x-auto">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup: any) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header: any) => {
                  const movable = !!layout && !layout.locked(header.column.id);
                  return (
                    <TableHead
                      key={header.id}
                      // Plain HTML drag and drop, no library. It is what a heading row needs
                      // and nothing more, and the up/down controls in the View menu are what
                      // answers for a thumb, where this gesture does not exist at all.
                      draggable={movable}
                      onDragStart={
                        movable
                          ? (e) => {
                              setDragging(header.column.id);
                              e.dataTransfer.effectAllowed = "move";
                              // Firefox starts no drag at all without something set here.
                              e.dataTransfer.setData("text/plain", header.column.id);
                            }
                          : undefined
                      }
                      onDragOver={
                        movable
                          ? (e) => {
                              if (!dragging || dragging === header.column.id) return;
                              // Without this the drop never fires: the default is to refuse.
                              e.preventDefault();
                              e.dataTransfer.dropEffect = "move";
                              setOver(header.column.id);
                            }
                          : undefined
                      }
                      onDragLeave={movable ? () => setOver(null) : undefined}
                      onDrop={
                        movable
                          ? (e) => {
                              e.preventDefault();
                              const from = dragging || e.dataTransfer.getData("text/plain");
                              if (from && from !== header.column.id)
                                layout?.dropColumn(from, header.column.id);
                              setDragging(null);
                              setOver(null);
                            }
                          : undefined
                      }
                      onDragEnd={() => {
                        setDragging(null);
                        setOver(null);
                      }}
                      className={cn(
                        "whitespace-nowrap",
                        movable && "cursor-grab select-none active:cursor-grabbing",
                        dragging === header.column.id && "opacity-40",
                        over === header.column.id &&
                          dragging !== header.column.id &&
                          "border-l-2 border-l-primary",
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
                  );
                })}
              </TableRow>
            ))}

            {/* A box under each heading, filtering that column alone.
                One search box over the whole table answers "is this person here"; narrowing a
                list down to one partner, one stage and one occupation at once is a different
                question, and it was being answered by exporting to Excel. */}
            {columnFilters ? (
              <TableRow className="hover:bg-transparent">
                {table.getVisibleLeafColumns().map((column: any) => {
                  const spec: ColumnFilterSpec | undefined =
                    column.columnDef.meta?.filter;

                  return (
                    <th
                      key={column.id}
                      className={cn(
                        "bg-muted/30 p-1 align-middle",
                        column.id === "actions" && PINNED
                      )}
                    >
                      {/* A funnel where the server will answer, and nothing where it will not.
                          A column with no field behind it used to get a box that narrowed the
                          page in the browser, which on a paginated board answers a different
                          question from the one being asked. */}
                      {serverFiltered ? (
                        spec ? (
                          <ColumnFilterCell
                            spec={spec}
                            heading={headingOf(column, table)}
                            value={filterFor(filters!, spec.field)}
                            onChange={(next) =>
                              onFiltersChange!(withFilter(filters!, spec.field, next))
                            }
                          />
                        ) : null
                      ) : column.getCanFilter() ? (
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
                  );
                })}
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
                    if (!rowClickOpensActions || !inThisRow(e)) return;
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
                    if (!inThisRow(e)) return;
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
