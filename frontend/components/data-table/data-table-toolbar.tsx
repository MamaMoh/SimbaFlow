"use client";

import * as React from "react";
import Link from "next/link";
import type { TableLayout } from "@/lib/data-table/use-table-layout";
import type { Table } from "@tanstack/react-table";

import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { DataTableViewOptions } from "@/components/data-table/data-table-view-options";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  ChevronDown,
  Download,
  PlusCircleIcon,
  Search,
  Table2,
  TrashIcon,
  X,
} from "lucide-react";
import { toast } from "sonner";
import { exportTableToCsv } from "@/components/data-table/export-csv";
import { useDebounce } from "@/hooks/use-debounce";
import {
  Tooltip,
  TooltipTrigger,
  TooltipContent,
  TooltipProvider,
} from "@/components/ui/tooltip";

/** Reusable tooltip wrapper */
interface AppTooltipProps {
  content?: string;
  children: React.ReactNode;
}

export function AppTooltip({ content, children }: AppTooltipProps) {
  if (!content) return <>{children}</>;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent sideOffset={2}>{content}</TooltipContent>
    </Tooltip>
  );
}

interface DataTableToolbarProps<TData> {
  table: Table<TData>;
  enableGlobalFilter?: boolean;
  newRowLink?: string;
  deleteRowsAction?: React.MouseEventHandler<HTMLButtonElement>;
  /** Whatever this board puts beside Export — its status switch, its Create button. */
  toolbarEndActions?: React.ReactNode;
  /** The board's Create button. Drawn last, after Export, because that is where it belongs. */
  createAction?: React.ReactNode;
  /** When set, Export offers this table as a CSV under this filename. */
  exportFileName?: string;
  /**
   * More ways out of this board, listed under Export.
   *
   * Downloading the paperwork for the ticked rows is an export, and it used to be a button of
   * its own sitting beside the one called Export. One menu, and the toolbar is shorter by a
   * button on every board that has one.
   */
  exportActions?: React.ReactNode;
  searchPlaceholder?: string;
  tooltips?: {
    delete?: string;
    new?: string;
    search?: Record<string, string>;
  };
  /** This board's remembered column arrangement, when it has one. */
  layout?: TableLayout;
}

/**
 * One row above every table, the same on all of them.
 *
 * Search and the column menu on the left, the board's own switches and the ways out on the
 * right. Nothing else: this row used to carry date-range boxes, a filter popover, per-column
 * dropdowns, a reset, a print button and a fullscreen toggle, none of which any board ever
 * passed — dead props that made the file four times this length and taught nobody anything.
 *
 * Narrowing a list is the column filter row's job, under the headings, where the filter sits on
 * the column it belongs to.
 */
export function DataTableToolbar<TData>({
  table,
  enableGlobalFilter = false,
  newRowLink,
  deleteRowsAction,
  toolbarEndActions,
  createAction,
  exportFileName,
  exportActions,
  searchPlaceholder = "Search…",
  tooltips = {},
  layout,
}: DataTableToolbarProps<TData>) {
  const globalFilterValue = table.getState().globalFilter;
  const [isPending, startTransition] = React.useTransition();
  const [globalSearch, setGlobalSearch] = React.useState(globalFilterValue || "");
  const debouncedGlobalSearch = useDebounce(globalSearch, 300);

  React.useEffect(() => {
    if (enableGlobalFilter) {
      table.setGlobalFilter(
        debouncedGlobalSearch === "" ? undefined : debouncedGlobalSearch,
      );
    }
  }, [debouncedGlobalSearch, table, enableGlobalFilter]);

  const selected = table.getSelectedRowModel().rows.length;

  return (
    <TooltipProvider delayDuration={200}>
      {/* Wraps rather than scrolls: on a narrow window the buttons on the right go down a line
          rather than off the edge, where only a scrollbar nobody saw would reach them. */}
      <div className="flex w-full flex-wrap items-center justify-between gap-3 border-b bg-card p-3">
        {/* Left: search, then the column menu beside it. */}
        <div className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
          {enableGlobalFilter && (
            <div className="relative flex items-center">
              <Search className="text-muted-foreground pointer-events-none absolute left-3 z-10 h-4 w-4" />
              <Input
                placeholder={searchPlaceholder}
                value={globalSearch}
                onChange={(e) => setGlobalSearch(e.target.value)}
                className="bg-background h-8 w-[180px] pr-9 pl-9 lg:w-[280px]"
              />
              {globalSearch.length > 0 && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="hover:bg-accent absolute top-1/2 right-1 z-10 h-6 w-6 -translate-y-1/2 rounded-full p-0"
                  onClick={() => setGlobalSearch("")}
                  aria-label="Clear search"
                >
                  <X className="text-muted-foreground h-3.5 w-3.5" />
                </Button>
              )}
            </div>
          )}

          <DataTableViewOptions table={table} layout={layout} />
        </div>

        {/* Right: this board's own switches, the ways out, and Create last. */}
        <div className="flex min-w-0 flex-1 flex-wrap items-center justify-end gap-2">
          {/* Only while rows are ticked, so it is never part of the resting toolbar. */}
          {deleteRowsAction && selected > 0 ? (
            <AppTooltip content={tooltips.delete}>
              <Button
                aria-label="Delete selected rows"
                variant="outline"
                size="sm"
                className="h-8"
                onClick={(event) => {
                  startTransition(() => {
                    table.toggleAllPageRowsSelected(false);
                    deleteRowsAction(event);
                  });
                }}
                disabled={isPending}
              >
                <TrashIcon className="mr-2 h-4 w-4" aria-hidden="true" />
                Delete
              </Button>
            </AppTooltip>
          ) : null}

          {toolbarEndActions}

          {(exportFileName || exportActions) && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button type="button" variant="outline" size="sm" className="h-8">
                  <Download className="mr-2 h-4 w-4" />
                  Export
                  <ChevronDown className="ml-1 h-3.5 w-3.5 opacity-60" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-[240px]">
                {exportFileName && (
                  <DropdownMenuItem
                    onClick={() => {
                      const n = exportTableToCsv(table, exportFileName);
                      toast.success(`Exported ${n} row${n === 1 ? "" : "s"}`);
                    }}
                  >
                    <Table2 className="mr-2 h-4 w-4" />
                    {/* Says what it will do before it does it: with rows ticked this exports
                        those and not the page. */}
                    {selected > 0
                      ? `This table as CSV (${selected})`
                      : "This table as CSV"}
                  </DropdownMenuItem>
                )}
                {exportFileName && exportActions ? <DropdownMenuSeparator /> : null}
                {exportActions}
              </DropdownMenuContent>
            </DropdownMenu>
          )}

          {createAction}

          {newRowLink ? (
            <AppTooltip content={tooltips.new}>
              <Link aria-label="Create new row" href={newRowLink}>
                <div
                  className={cn(
                    buttonVariants({ variant: "outline", size: "sm", className: "h-8" }),
                  )}
                >
                  <PlusCircleIcon className="mr-2 h-4 w-4" aria-hidden="true" />
                  New
                </div>
              </Link>
            </AppTooltip>
          ) : null}
        </div>
      </div>
    </TooltipProvider>
  );
}
