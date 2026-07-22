"use client";

import * as React from "react";
import { flexRender, type Table as TanStackTable } from "@tanstack/react-table";
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

type PaginationProps = Omit<React.ComponentProps<typeof DataTablePagination>, "table">;

export interface DataTableProps<TData, TValue> {
  table: TanStackTable<TData>;
  filterableColumns?: any[];
  searchableColumns?: any[];
  enableGlobalFilter?: boolean;
  newRowLink?: string;
  deleteRowsAction?: React.MouseEventHandler<HTMLButtonElement>;
  paginated?: boolean;
  viewHidden?: boolean;
  paginationProps?: PaginationProps;
  toolbarEndActions?: React.ReactNode;
  isFullscreen?: boolean;
  onToggleFullscreen?: () => void;
  onPrint?: (allData: any[], selectedRows: any[]) => void;
  searchPlaceholder?: string;
  useFilterPopover?: boolean;
  dense?: boolean;
  emptyMessage?: string;
}

export function DataTable<TData, TValue>(props: DataTableProps<TData, TValue>) {
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
    isFullscreen,
    onToggleFullscreen,
    onPrint,
    searchPlaceholder,
    useFilterPopover = false,
    dense = false,
    emptyMessage = "No results.",
  } = props;

  return (
    <div
      className={cn(
        "w-full space-y-3",
        isFullscreen ? "fixed inset-0 z-50 bg-background p-3 overflow-auto" : "",
      )}
    >
      {!viewHidden && (
        <DataTableToolbar
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
      <div className="rounded-lg border bg-card shadow-sm overflow-hidden">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className="bg-muted/40 hover:bg-muted/40">
                {headerGroup.headers.map((header) => (
                  <TableHead
                    key={header.id}
                    className={cn(
                      "whitespace-nowrap text-[11px] font-semibold uppercase tracking-wide text-muted-foreground",
                      dense && "h-9 px-2",
                    )}
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows?.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && "selected"}
                  className="hover:bg-muted/30"
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className={cn(dense && "py-2 px-2 text-[13px]")}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={table.getVisibleLeafColumns().length}
                  className="h-28 text-center text-muted-foreground"
                >
                  {emptyMessage}
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
