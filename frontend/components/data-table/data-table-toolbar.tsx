"use client";

import * as React from "react";
import Link from "next/link";
import type {
  DataTableFilterableColumn,
  DataTableSearchableColumn,
} from "@/types";
import type { Table } from "@tanstack/react-table";

import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { DataTableFacetedFilter } from "@/components/data-table/data-table-faceted-filter";
import { DataTableViewOptions } from "@/components/data-table/data-table-view-options";
import { Combobox } from "@/components/ui/combobox";
import {
  CrossIcon,
  PlusCircleIcon,
  TrashIcon,
  Maximize2,
  Minimize2,
  PrinterIcon,
  X,
  Search,
  Filter,
  Calendar,
  User,
  Stethoscope,
  CheckCircle2,
} from "lucide-react";
import { useDebounce } from "@/hooks/use-debounce";
import {
  Tooltip,
  TooltipTrigger,
  TooltipContent,
  TooltipProvider,
} from "@/components/ui/tooltip";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../ui/select";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Checkbox } from "@/components/ui/checkbox";

/** Reusable tooltip wrapper */
interface AppTooltipProps {
  content?: string;
  children: React.ReactNode;
  sideOffset?: number;
}
export function AppTooltip({
  content,
  children,
  sideOffset = 2,
}: AppTooltipProps) {
  if (!content) return <>{children}</>;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent sideOffset={sideOffset}>{content}</TooltipContent>
    </Tooltip>
  );
}

interface DataTableToolbarProps<TData> {
  table: Table<TData>;
  filterableColumns?: DataTableFilterableColumn<TData>[];
  searchableColumns?: DataTableSearchableColumn<TData>[];
  enableGlobalFilter?: boolean;
  newRowLink?: string;
  deleteRowsAction?: React.MouseEventHandler<HTMLButtonElement>;
  toolbarEndActions?: React.ReactNode;
  onToggleFullscreen?: () => void;
  isFullscreen?: boolean;
  onPrint?: (allData: any[], selectedRows: any[]) => void;
  searchPlaceholder?: string;
  tooltips?: {
    delete?: string;
    new?: string;
    print?: string;
    fullscreen?: string;
    search?: Record<string, string>;
    filters?: Record<string, string>;
    reset?: string;
  };
  /** When true, show a single Filter button that opens a popover with checklist for each filter column (Status, Doctor) */
  useFilterPopover?: boolean;
}

export function DataTableToolbar<TData>({
  table,
  filterableColumns = [],
  searchableColumns = [],
  enableGlobalFilter = false,
  newRowLink,
  deleteRowsAction,
  toolbarEndActions,
  onToggleFullscreen,
  isFullscreen,
  onPrint,
  searchPlaceholder = "Search appointments...",
  tooltips = {},
  useFilterPopover = false,
}: DataTableToolbarProps<TData>) {
  const isFiltered = table.getState().columnFilters.length > 0;
  const globalFilterValue = table.getState().globalFilter;
  const [isPending, startTransition] = React.useTransition();
  const [globalSearch, setGlobalSearch] = React.useState(
    globalFilterValue || ""
  );
  const debouncedGlobalSearch = useDebounce(globalSearch, 300);

  const [searchInputs, setSearchInputs] = React.useState<
    Record<string, string>
  >(() =>
    Object.fromEntries(
      searchableColumns.map((col) => [
        String(col.id),
        (table.getColumn(String(col.id))?.getFilterValue() as string) ?? "",
      ])
    )
  );

  const debouncedSearchInputs = useDebounce(searchInputs, 300);

  // Update global filter
  React.useEffect(() => {
    if (enableGlobalFilter) {
      table.setGlobalFilter(
        debouncedGlobalSearch === "" ? undefined : debouncedGlobalSearch
      );
    }
  }, [debouncedGlobalSearch, table, enableGlobalFilter]);

  // Update table filters after debounce — only if the value actually changed
  React.useEffect(() => {
    Object.entries(debouncedSearchInputs).forEach(([id, value]) => {
      const col = table.getColumn(id);
      if (!col) return;

      const current = col.getFilterValue() as any;
      const newValue = value === "" ? undefined : value;

      // deep-equal for possible array/object, otherwise simple equality
      const isEqual =
        typeof current === "object" && current !== null
          ? JSON.stringify(current) === JSON.stringify(newValue)
          : current === newValue;

      if (!isEqual) {
        col.setFilterValue(newValue);
      }
    });
  }, [debouncedSearchInputs, table]);

  return (
    <TooltipProvider delayDuration={200}>
      <div className="flex w-full items-center justify-between gap-3 overflow-auto p-3 border-b bg-card">
          {/* Left section: search + filters */}
          <div className="flex flex-1 items-center gap-2 flex-wrap">
          {/* Global search */}
          {enableGlobalFilter && (
            <div className="relative flex items-center">
              <Search className="absolute left-3 h-4 w-4 text-muted-foreground pointer-events-none z-10" />
              <Input
                placeholder={searchPlaceholder}
                value={globalSearch}
                onChange={(e) => setGlobalSearch(e.target.value)}
                className="h-9 w-[180px] lg:w-[280px] pl-9 pr-9 bg-background"
              />
              {globalSearch && globalSearch.length > 0 && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="absolute right-1 top-1/2 -translate-y-1/2 z-10 h-6 w-6 rounded-full p-0 hover:bg-accent"
                  onClick={() => setGlobalSearch("")}
                  aria-label="Clear search"
                >
                  <X className="h-3.5 w-3.5 text-muted-foreground" />
                </Button>
              )}
            </div>
          )}

          {/* Searchable columns (Date filters) */}
          {!enableGlobalFilter &&
            searchableColumns.length > 0 && (
              <div className="flex items-center gap-2.5 pl-3 border-l border-border/50">
                <div className="flex items-center gap-1 text-xs text-muted-foreground">
                  <Calendar className="h-3.5 w-3.5" />
                  <span className="font-medium">Date Range:</span>
                </div>
                {searchableColumns.map((column) => {
                  const columnId = String(column.id);
                  if (!table.getColumn(columnId)) return null;
                  const isDate = (column as any).inputType === "datetime-local" || 
                                 (column as any).inputType === "date";
                  const isDateRange = columnId.toLowerCase().includes("from") || 
                                     columnId.toLowerCase().includes("to");
                  const hasValue = searchInputs[columnId] && searchInputs[columnId] !== "";

                  return (
                    <AppTooltip
                      key={columnId}
                      content={tooltips.search?.[columnId]}
                    >
                      <div className="relative">
                        {isDate && (
                          <Calendar className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
                        )}
                        <Input
                          placeholder={isDateRange ? column.title : `Filter ${column.title}...`}
                          type={(column as any).inputType ?? "text"}
                          value={searchInputs[columnId] ?? ""}
                          onChange={(e) =>
                            setSearchInputs((prev) => ({
                              ...prev,
                              [columnId]: e.target.value,
                            }))
                          }
                          className={cn(
                            "h-9 bg-background",
                            isDate ? "w-[160px] pl-8 pr-8" : "w-[150px] lg:w-[200px]",
                            hasValue && "bg-primary/5 border-primary/20"
                          )}
                        />
                        {hasValue && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="absolute right-1 top-1/2 -translate-y-1/2 h-6 w-6 rounded-full p-0 hover:bg-accent"
                            onClick={() => {
                              setSearchInputs((prev) => ({
                                ...prev,
                                [columnId]: "",
                              }));
                            }}
                            aria-label="Clear date filter"
                          >
                            <X className="h-3 w-3 text-muted-foreground" />
                          </Button>
                        )}
                      </div>
                    </AppTooltip>
                  );
                })}
              </div>
            )}

          {/* Filterable columns: single Filter popover (checklists) or inline Combobox/Select */}
          {filterableColumns.length > 0 && useFilterPopover && (
            <div className="flex items-center gap-2.5 pl-3 border-l border-border/50">
              <Popover>
                <PopoverTrigger asChild>
                  <Button
                    variant="outline"
                    size="sm"
                    className={cn(
                      "h-9 gap-2",
                      isFiltered && "bg-primary/10 border-primary/20"
                    )}
                    aria-label="Open filters"
                  >
                    <Filter className="h-4 w-4" />
                    Filters
                    {isFiltered && (
                      <span className="ml-1 flex h-5 min-w-[20px] items-center justify-center rounded-full bg-primary px-1.5 text-xs text-primary-foreground">
                        {table.getState().columnFilters.filter((f) =>
                          filterableColumns.some((c) => String(c.id) === f.id)
                        ).length}
                      </span>
                    )}
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[280px] p-0" align="start">
                  <div className="flex items-center justify-between border-b px-3 py-2">
                    <span className="text-sm font-semibold">Filters</span>
                    <Button
                      variant="link"
                      size="sm"
                      className="h-auto p-0 text-muted-foreground hover:text-foreground"
                      onClick={() => {
                        table.resetColumnFilters();
                        setSearchInputs({});
                      }}
                    >
                      Reset
                    </Button>
                  </div>
                  <div className="max-h-[320px] overflow-y-auto py-2">
                    {filterableColumns.map((column) => {
                      const colObj = table.getColumn(String(column.id));
                      if (!colObj) return null;
                      const current = (colObj.getFilterValue() as string[]) || [];
                      const selectedSet = new Set(current);
                      const options = column.options || [];
                      return (
                        <div
                          key={String(column.id)}
                          className="space-y-2 px-3 py-2"
                        >
                          <div className="text-xs font-medium text-muted-foreground">
                            {column.title}
                          </div>
                          <div className="space-y-1.5">
                            {options.map((opt: { value: string; label: string }) => {
                              const checked = selectedSet.has(String(opt.value));
                              return (
                                <label
                                  key={opt.value}
                                  className="flex cursor-pointer items-center gap-2 rounded-sm py-1.5 pr-2 hover:bg-muted/50"
                                >
                                  <Checkbox
                                    checked={checked}
                                    onCheckedChange={(checked) => {
                                      const next = checked
                                        ? [...current, String(opt.value)]
                                        : current.filter((v) => v !== opt.value);
                                      colObj.setFilterValue(
                                        next.length ? next : undefined
                                      );
                                    }}
                                  />
                                  <span className="text-sm">{opt.label}</span>
                                </label>
                              );
                            })}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </PopoverContent>
              </Popover>
            </div>
          )}

          {filterableColumns.length > 0 && !useFilterPopover && (
            <div className="flex items-center gap-2.5 pl-3 border-l border-border/50">
              {filterableColumns.map((column) => {
                const colObj = table.getColumn(String(column.id));
                if (!colObj) return null;

                const getIcon = () => {
                  const colId = String(column.id).toLowerCase();
                  if (colId.includes("office") || colId.includes("agent")) {
                    return <Stethoscope className="h-3.5 w-3.5 text-muted-foreground" />;
                  }
                  if (colId.includes("candidate")) {
                    return <User className="h-3.5 w-3.5 text-muted-foreground" />;
                  }
                  if (colId.includes("status")) {
                    return <CheckCircle2 className="h-3.5 w-3.5 text-muted-foreground" />;
                  }
                  return <Filter className="h-3.5 w-3.5 text-muted-foreground" />;
                };

                const useCombobox = ["office", "candidate"].includes(String(column.id));
                const current = colObj.getFilterValue() as string[] | undefined;
                const selected =
                  Array.isArray(current) && current.length ? current[0] : "";
                const options = column.options || [];
                const isFiltered = selected && selected !== "";

                if (useCombobox) {
                  return (
                    <AppTooltip
                      key={String(column.id)}
                      content={tooltips.filters?.[String(column.id)]}
                    >
                      <div className="relative">
                        <Combobox
                          items={options.map((opt: any) => ({
                            value: opt.value,
                            label: opt.label,
                          }))}
                          value={selected || ""}
                          onValueChange={(val: string) => {
                            if (!val) {
                              colObj.setFilterValue(undefined);
                            } else {
                              colObj.setFilterValue([val]);
                            }
                          }}
                          placeholder={column.title}
                          className={cn(
                            "h-9 w-[160px] lg:w-[180px]",
                            isFiltered && "bg-primary/10 border-primary/20 pr-8"
                          )}
                          disabled={false}
                        />
                        {isFiltered && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="absolute right-8 top-1/2 -translate-y-1/2 h-6 w-6 rounded-full p-0 hover:bg-accent z-10 flex items-center justify-center"
                            onClick={(e) => {
                              e.stopPropagation();
                              e.preventDefault();
                              colObj.setFilterValue(undefined);
                            }}
                            aria-label="Clear filter"
                          >
                            <X className="h-3 w-3 text-muted-foreground" />
                          </Button>
                        )}
                      </div>
                    </AppTooltip>
                  );
                }

                if (String(column.id) === "status") {
                  return (
                    <AppTooltip
                      key={String(column.id)}
                      content={tooltips.filters?.[String(column.id)]}
                    >
                      <div className="relative">
                        <Select
                          value={selected}
                          onValueChange={(val: string) => {
                            if (val === "__CLEAR__" || !val) {
                              colObj.setFilterValue(undefined);
                            } else {
                              colObj.setFilterValue([val]);
                            }
                          }}
                        >
                          <SelectTrigger
                            size="sm"
                            className={cn(
                              "h-9 w-[140px] lg:w-[160px]",
                              isFiltered && "bg-primary/10 border-primary/20"
                            )}
                          >
                            <div className="flex items-center gap-2">
                              {getIcon()}
                              <SelectValue placeholder={String(column.title)} />
                            </div>
                          </SelectTrigger>
                          <SelectContent className="max-h-[300px]">
                            {isFiltered && (
                              <SelectItem value="__CLEAR__" className="text-muted-foreground">
                                Clear filter
                              </SelectItem>
                            )}
                            {options.map((opt: any) => (
                              <SelectItem key={opt.value} value={opt.value}>
                                {opt.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </AppTooltip>
                  );
                }

                return (
                  <AppTooltip
                    key={String(column.id)}
                    content={tooltips.filters?.[String(column.id)]}
                  >
                    <DataTableFacetedFilter
                      column={colObj}
                      title={column.title}
                      options={column.options}
                    />
                  </AppTooltip>
                );
              })}
            </div>
          )}

            {/* Reset filters */}
            {isFiltered && (
              <AppTooltip content={tooltips.reset}>
                <Button
                  aria-label="Reset filters"
                  variant="outline"
                  size="sm"
                  className="h-9 gap-2 text-muted-foreground hover:text-foreground"
                  onClick={() => {
                    table.resetColumnFilters();
                    setSearchInputs({});
                    setGlobalSearch("");
                  }}
                >
                  <CrossIcon className="h-4 w-4" aria-hidden="true" />
                  Reset
                </Button>
              </AppTooltip>
            )}
          </div>

        {/* Right section: actions */}
        <div className="flex items-center gap-2">
          {deleteRowsAction && table.getSelectedRowModel().rows.length > 0 ? (
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
          ) : newRowLink ? (
            <AppTooltip content={tooltips.new}>
              <Link aria-label="Create new row" href={newRowLink}>
                <div
                  className={cn(
                    buttonVariants({
                      variant: "outline",
                      size: "sm",
                      className: "h-8",
                    })
                  )}
                >
                  <PlusCircleIcon className="mr-2 h-4 w-4" aria-hidden="true" />
                  New
                </div>
              </Link>
            </AppTooltip>
          ) : null}

          {/* View options */}
          <DataTableViewOptions table={table} />

          {/* Print */}
          {onPrint && (
            <AppTooltip content={tooltips.print}>
              <Button
                aria-label="Print table"
                variant="outline"
                size="sm"
                className="h-8"
                onClick={() =>
                  onPrint(
                    table.getCoreRowModel().rows,
                    table.getSelectedRowModel().rows
                  )
                }
              >
                <PrinterIcon className="h-4 w-4" aria-hidden="true" />
              </Button>
            </AppTooltip>
          )}

          {toolbarEndActions}

          {/* Fullscreen */}
          {onToggleFullscreen && (
            <AppTooltip content={tooltips.fullscreen}>
              <Button
                aria-label={
                  isFullscreen ? "Exit fullscreen" : "Enter fullscreen"
                }
                variant="outline"
                size="sm"
                className="h-8"
                onClick={onToggleFullscreen}
              >
                {isFullscreen ? (
                  <Minimize2 className="h-4 w-4" aria-hidden="true" />
                ) : (
                  <Maximize2 className="h-4 w-4" aria-hidden="true" />
                )}
              </Button>
            </AppTooltip>
          )}
        </div>
      </div>
    </TooltipProvider>
  );
}
