"use client";

import { useMemo, useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { accountingApi, useExchangeRates, type ExchangeRateRow } from "@/lib/api/accounting";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2, Plus } from "lucide-react";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";
import { indexColumn } from "@/components/data-table/index-column";

export default function ExchangeRatesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("accounting.read") || hasPermission("system.admin");
  const canPost = hasPermission("accounting.post") || hasPermission("system.admin");

  const { rates: all, isLoading, error, mutate } = useExchangeRates({ toCurrency: "ETB" });
  const [sorting, setSorting] = useState<SortingState>([]);
  // The same search box and the same funnels as every other board. The rates arrive whole —
  // there is no page two — so the rows in hand are all the rows there are, and the filters are
  // answered here rather than asked of the API.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const rates = useMemo(
    () =>
      applyColumnFilters(all, filters, {
        fromCurrency: "text",
        toCurrency: "text",
        rate: "number",
        effectiveDate: "date",
        source: "text",
      }),
    [all, filters],
  );
  const [open, setOpen] = useState(false);
  const [fromCurrency, setFromCurrency] = useState("USD");
  const [rate, setRate] = useState("1");
  const [effectiveDate, setEffectiveDate] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [busy, setBusy] = useState(false);

  const columns = useMemo<ColumnDef<ExchangeRateRow>[]>(
    () => [
      indexColumn<ExchangeRateRow>(),
      {
        accessorKey: "fromCurrency",
        meta: { filter: { field: "fromCurrency", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="From" />,
      },
      {
        accessorKey: "toCurrency",
        meta: { filter: { field: "toCurrency", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="To" />,
      },
      {
        accessorKey: "rate",
        meta: { filter: { field: "rate", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Rate" />,
        cell: ({ getValue }) => Number(getValue()).toFixed(6),
      },
      {
        accessorKey: "effectiveDate",
        meta: { filter: { field: "effectiveDate", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Effective" />,
      },
      {
        id: "scope",
        accessorFn: (row) => (row.isAgencyRate ? "Your agency" : "Platform default"),
        header: ({ column }) => <DataTableColumnHeader column={column} title="Set by" />,
      },
      {
        accessorKey: "source",
        meta: { filter: { field: "source", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Source" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
    ],
    []
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "exchange-rates", columnIds });

  const table = useReactTable({
    data: rates,
    columns,
    state: {
      sorting,
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  const submit = async () => {
    const value = Number(rate);
    if (!(value > 0)) {
      toast.error("Rate must be > 0");
      return;
    }
    setBusy(true);
    try {
      await accountingApi.upsertRate({
        fromCurrency,
        toCurrency: "ETB",
        rate: value,
        effectiveDate,
        source: "manual",
      });
      toast.success("Exchange rate saved");
      setOpen(false);
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to save rate");
    } finally {
      setBusy(false);
    }
  };

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="exchange rates" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Exchange rates" />

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : null}


      {!error ? (
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          <DataTable
            layout={layout}
            table={table}
            paginated
            enableGlobalFilter
            searchPlaceholder="Search rates…"
            exportFileName="exchange-rates"
            filters={filters}
            onFiltersChange={setFilters}
            createAction={
              canPost ? (
                <Button
                  size="sm"
                  className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
                  onClick={() => setOpen(true)}
                >
                  <Plus className="h-3.5 w-3.5" />
                  Add rate
                </Button>
              ) : null
            }
            emptyMessage="No exchange rates yet. Add one before recording a payment in another currency."
          />
        </div>
      ) : null}


      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent className="w-[400px] sm:max-w-[400px] flex flex-col gap-4">
          <SheetHeader>
            <SheetTitle>Add exchange rate</SheetTitle>
            <SheetDescription>
              Saved for your agency only. It replaces your own rate for the same pair and date,
              and is used instead of the platform default from that date on.
            </SheetDescription>
          </SheetHeader>
          <div className="px-6 space-y-3">
            <div className="space-y-1.5">
              <Label>From currency</Label>
              <Input
                value={fromCurrency}
                maxLength={3}
                onChange={(e) => setFromCurrency(e.target.value.toUpperCase())}
              />
            </div>
            <div className="space-y-1.5">
              <Label>To currency</Label>
              <Input value="ETB" disabled />
            </div>
            <div className="space-y-1.5">
              <Label>Rate</Label>
              <Input
                type="number"
                min={0.000001}
                step="0.000001"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Effective date</Label>
              <Input
                type="date"
                value={effectiveDate}
                onChange={(e) => setEffectiveDate(e.target.value)}
              />
            </div>
          </div>
          <Button
            className="mx-6 mb-6 mt-auto bg-green-800 hover:bg-green-900 text-white"
            disabled={busy}
            onClick={submit}
          >
            {busy ? "Saving…" : "Save"}
          </Button>
        </SheetContent>
      </Sheet>
    </div>
  );
}
