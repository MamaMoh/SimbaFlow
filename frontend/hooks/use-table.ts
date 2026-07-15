import * as React from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  getCoreRowModel,
  getFacetedRowModel,
  getFacetedUniqueValues,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type ColumnFiltersState,
  type PaginationState,
  type SortingState,
  type VisibilityState,
} from "@tanstack/react-table";
import { useDebounce } from "@/hooks/use-debounce";

export interface UseTableOptions<TData, TValue> {
  columns: ColumnDef<TData, TValue>[];
  data: TData[];
  pageCount?: number;
  filterableColumns?: any[];
  searchableColumns?: any[];
  syncQueryString?: boolean;
  initialFilters?: ColumnFiltersState;
  onStateChange?: (state: {
    pagination: PaginationState;
    columnFilters: ColumnFiltersState;
    sorting: SortingState;
  }) => void;
  serverSide?: boolean;
  paginated?: boolean;
  getRowId?: (row: TData) => string; // NEW: unique ID per row
  /** When set, global filter is synced to this URL query param (e.g. "searchTerm") for server-side search */
  searchParamKeyForGlobalFilter?: string;
}

export function useTable<TData, TValue>({
  columns,
  data,
  pageCount,
  filterableColumns = [],
  searchableColumns = [],
  syncQueryString = true,
  initialFilters,
  onStateChange,
  serverSide = false,
  paginated = true,
  getRowId,
  searchParamKeyForGlobalFilter,
}: UseTableOptions<TData, TValue>) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // --- URL Parsing ---
  const page = searchParams?.get("page") ?? "1";
  const pageAsNumber = Number(page);
  const fallbackPage =
    isNaN(pageAsNumber) || pageAsNumber < 1 ? 1 : pageAsNumber;

  const per_page = searchParams?.get("per_page") ?? "10";
  const perPageAsNumber = Number(per_page);
  const fallbackPerPage = isNaN(perPageAsNumber) ? 10 : perPageAsNumber;

  const initialGlobalFilter =
    searchParamKeyForGlobalFilter && searchParams
      ? searchParams.get(searchParamKeyForGlobalFilter) ?? ""
      : "";

  const createQueryString = React.useCallback(
    (params: Record<string, string | number | null>) => {
      const newSearchParams = new URLSearchParams(searchParams?.toString());
      for (const [key, value] of Object.entries(params)) {
        if (value === null || value === "" || value === undefined) {
          newSearchParams.delete(key);
        } else {
          newSearchParams.set(key, String(value));
        }
      }
      return newSearchParams.toString();
    },
    [searchParams]
  );

  // --- State ---
  const [rowSelection, setRowSelection] = React.useState({});
  const [columnVisibility, setColumnVisibility] =
    React.useState<VisibilityState>({});
  const [columnFilters, setColumnFilters] = React.useState<ColumnFiltersState>(
    initialFilters ?? []
  );
  const [globalFilter, setGlobalFilter] = React.useState(initialGlobalFilter);
  const [sorting, setSorting] = React.useState<SortingState>([]);
  const [{ pageIndex, pageSize }, setPagination] =
    React.useState<PaginationState>({
      pageIndex: fallbackPage - 1,
      pageSize: fallbackPerPage,
    });

  const pagination = React.useMemo(
    () => ({ pageIndex, pageSize }),
    [pageIndex, pageSize]
  );

  // --- Sync from URL on mount/navigation ---
  const didSyncFromUrl = React.useRef(false);
  React.useEffect(() => {
    const pageParam = searchParams?.get("page") ?? "1";
    const pageAsNumber = Number(pageParam);
    const urlPageIndex =
      (isNaN(pageAsNumber) || pageAsNumber < 1 ? 1 : pageAsNumber) - 1;

    const perPageParam = searchParams?.get("per_page") ?? "10";
    const perPageAsNumber = Number(perPageParam);
    const urlPageSize = isNaN(perPageAsNumber) ? 10 : perPageAsNumber;

    if (
      !didSyncFromUrl.current ||
      pageIndex !== urlPageIndex ||
      pageSize !== urlPageSize
    ) {
      setPagination({ pageIndex: urlPageIndex, pageSize: urlPageSize });
      didSyncFromUrl.current = true;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  // --- Sync pagination to URL ---
  React.useEffect(() => {
    if (paginated && syncQueryString) {
      const qs = createQueryString({ page: pageIndex + 1, per_page: pageSize });
      if (Number(page) !== pageIndex + 1 || Number(per_page) !== pageSize) {
        router.push(`${pathname}?${qs}`, { scroll: false });
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageIndex, pageSize]);

  // --- Sync global filter to URL for server-side search ---
  const debouncedGlobalFilter = useDebounce(globalFilter, 400);
  React.useEffect(() => {
    if (!searchParamKeyForGlobalFilter || !syncQueryString) return;
    const qs = createQueryString({
      page: 1,
      per_page: pageSize,
      [searchParamKeyForGlobalFilter]: debouncedGlobalFilter || null,
    });
    if (searchParams?.get(searchParamKeyForGlobalFilter) !== (debouncedGlobalFilter || null)) {
      router.push(`${pathname}?${qs}`, { scroll: false });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedGlobalFilter, searchParamKeyForGlobalFilter]);

  // --- Filters ---
  const debouncedSearchableColumnFilters = JSON.parse(
    useDebounce(
      JSON.stringify(
        columnFilters.filter((filter) =>
          searchableColumns.find((column) => column.id === filter.id)
        )
      ),
      500
    )
  ) as ColumnFiltersState;

  const filterableColumnFilters = columnFilters.filter((filter) =>
    filterableColumns.find((column) => column.id === filter.id)
  );

  // searchable → reset page
  const prevSearchValuesRef = React.useRef<Record<string, string>>({});
  React.useEffect(() => {
    if (!syncQueryString) return;

    if (debouncedSearchableColumnFilters.length === 0) {
      const paramsToRemove = searchableColumns.map((c) => c.queryKey || c.id);
      const newSearchParams = new URLSearchParams(searchParams?.toString());
      paramsToRemove.forEach((key) => newSearchParams.delete(String(key)));
      if (newSearchParams.toString() !== searchParams.toString()) {
        router.push(`${pathname}?${newSearchParams.toString()}`, {
          scroll: false,
        });
      }
      prevSearchValuesRef.current = {};
      return;
    }

    debouncedSearchableColumnFilters.forEach((column) => {
      if (typeof column.value === "string") {
        const paramKey =
          searchableColumns.find((c) => String(c.id) === column.id)?.queryKey ||
          column.id;
        const prevValue = prevSearchValuesRef.current[paramKey];
        const currentValue = column.value;

        const qs = createQueryString({
          page: 1,
          [paramKey]: currentValue || null,
        });
        if (qs !== searchParams.toString() && prevValue !== currentValue) {
          router.push(`${pathname}?${qs}`, { scroll: false });
        }
        prevSearchValuesRef.current[paramKey] = currentValue;
      }
    });
  }, [
    debouncedSearchableColumnFilters,
    pathname,
    router,
    searchParams,
    createQueryString,
    searchableColumns,
    syncQueryString,
  ]);

  // filterable → keep current page
  React.useEffect(() => {
    if (!syncQueryString) return;

    if (filterableColumnFilters.length === 0) {
      const paramsToRemove = filterableColumns.map((c) => c.queryKey || c.id);
      const newSearchParams = new URLSearchParams(searchParams?.toString());
      paramsToRemove.forEach((key) => newSearchParams.delete(String(key)));
      if (newSearchParams.toString() !== searchParams.toString()) {
        router.push(`${pathname}?${newSearchParams.toString()}`, {
          scroll: false,
        });
      }
      return;
    }

    filterableColumnFilters.forEach((column) => {
      if (Array.isArray(column.value)) {
        const paramKey =
          filterableColumns.find((c) => String(c.id) === column.id)?.queryKey ||
          column.id;
        const qs = createQueryString({
          [paramKey]: column.value.length ? column.value.join(".") : null,
        });
        if (qs !== searchParams.toString()) {
          router.push(`${pathname}?${qs}`, { scroll: false });
        }
      }
    });
  }, [
    filterableColumnFilters,
    pathname,
    router,
    searchParams,
    createQueryString,
    filterableColumns,
    syncQueryString,
  ]);

  // --- Table Instance ---
  const memoColumns = React.useMemo(() => columns, [columns]);

  // NEW: Stable row IDs
  const stableGetRowId = React.useCallback(
    (row: TData, index: number) => {
      if (getRowId) return getRowId(row);
      if ((row as any).id !== undefined) return String((row as any).id);
      return String(index);
    },
    [getRowId]
  );

  // Global filter function for searching across all columns
  const globalFilterFn = React.useCallback((row: any, columnId: string, filterValue: string) => {
    if (!filterValue) return true;
    const searchValue = filterValue.toLowerCase();
    
    // Search across all visible columns
    const searchableValues = Object.values(row.original || {}).map((val: any) => {
      if (val === null || val === undefined) return "";
      if (typeof val === "object") {
        // Handle nested objects (patient, physician)
        if (val.firstName || val.lastName) {
          return `${val.firstName || ""} ${val.lastName || ""}`.toLowerCase();
        }
        return JSON.stringify(val).toLowerCase();
      }
      return String(val).toLowerCase();
    });
    
    return searchableValues.some((val: string) => val.includes(searchValue));
  }, []);

  const table = useReactTable({
    data,
    columns: memoColumns,
    // Provide pageCount ONLY for server-side pagination; use at least 1 to avoid "Page 1 of 0"
    pageCount: serverSide ? Math.max(1, pageCount ?? 0) : undefined,
    state: {
      pagination,
      columnVisibility,
      rowSelection,
      columnFilters,
      globalFilter,
      sorting,
    },
    getRowId: stableGetRowId, // Ensures row selection operates on unique row identifiers
    enableRowSelection: true,
    onRowSelectionChange: setRowSelection,
    onPaginationChange: setPagination,
    onColumnFiltersChange: setColumnFilters,
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    onColumnVisibilityChange: setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFacetedRowModel: getFacetedRowModel(),
    getFacetedUniqueValues: getFacetedUniqueValues(),
    manualPagination: serverSide,
    manualSorting: serverSide,
    manualFiltering: serverSide,
    globalFilterFn: globalFilterFn,
    autoResetPageIndex: false,
  });

  // --- Callbacks ---
  React.useEffect(() => {
    onStateChange?.({ pagination, columnFilters, sorting });
  }, [pagination, columnFilters, sorting, onStateChange]);

  // sorting → keep current page
  React.useEffect(() => {
    if (!syncQueryString) return;

    if (!sorting.length) {
      const qs = createQueryString({ sort: null, order: null });
      if (qs !== searchParams.toString())
        router.push(`${pathname}?${qs}`, { scroll: false });
      return;
    }
    const [primary] = sorting;
    const mappedKey =
      searchableColumns.find((c) => String(c.id) === primary.id)?.queryKey ||
      filterableColumns.find((c) => String(c.id) === primary.id)?.queryKey ||
      primary.id;
    const qs = createQueryString({
      sort: String(mappedKey),
      order: primary.desc ? "desc" : "asc",
    });
    if (qs !== searchParams.toString())
      router.push(`${pathname}?${qs}`, { scroll: false });
  }, [
    sorting,
    syncQueryString,
    pathname,
    router,
    searchParams,
    createQueryString,
    searchableColumns,
    filterableColumns,
  ]);

  return {
    table,
    state: {
      pagination,
      columnVisibility,
      rowSelection,
      columnFilters,
      sorting,
    },
    setPagination,
    setColumnFilters,
    setSorting,
    setColumnVisibility,
    setRowSelection,
  };
}
