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
import { Inbox } from "lucide-react";

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
  isFullscreen?: boolean;
  onToggleFullscreen?: () => void;
  onPrint?: (allData: any[], selectedRows: any[]) => void;
  searchPlaceholder?: string;
  useFilterPopover?: boolean;
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
    isFullscreen,
    onToggleFullscreen,
    onPrint,
    searchPlaceholder,
    useFilterPopover = false,
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
                  <TableHead key={header.id} className="whitespace-nowrap">
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
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows?.length ? (
              table.getRowModel().rows.map((row: any) => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && "selected"}
                  className={onRowClick ? "cursor-pointer" : undefined}
                  onClick={(e) => {
                    if (!onRowClick) return;
                    // Don't hijack clicks on interactive controls inside the row.
                    if ((e.target as HTMLElement).closest(
                      'button, a, input, label, [role="checkbox"], [role="menuitem"], [data-no-row-click]'
                    )) return;
                    onRowClick(row.original as TData);
                  }}
                >
                  {row.getVisibleCells().map((cell: any) => (
                    <TableCell key={cell.id}>
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
