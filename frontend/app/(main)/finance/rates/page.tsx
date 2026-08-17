"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
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
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import {
  accountingApi,
  useExchangeRates,
  type ExchangeRateRow,
} from "@/lib/api/accounting";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2, Plus } from "lucide-react";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";
import { indexColumn } from "@/components/data-table/index-column";

export default function ExchangeRatesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("accounting.read") || hasPermission("system.admin");
  const canPost = hasPermission("accounting.post") || hasPermission("system.admin");

  const { rates, isLoading, error, mutate } = useExchangeRates({ toCurrency: "ETB" });
  const [sorting, setSorting] = useState<SortingState>([]);
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
        header: ({ column }) => <DataTableColumnHeader column={column} title="From" />,
      },
      {
        accessorKey: "toCurrency",
        header: ({ column }) => <DataTableColumnHeader column={column} title="To" />,
      },
      {
        accessorKey: "rate",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Rate" />,
        cell: ({ getValue }) => Number(getValue()).toFixed(6),
      },
      {
        accessorKey: "effectiveDate",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Effective" />,
      },
      {
        accessorKey: "source",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Source" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
    ],
    []
  );

  const table = useReactTable({
    data: rates,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
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

  if (!canView) return <AccessDenied resource="Exchange rates" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Exchange rates"
        description="Platform rates used when recording non-ETB commission payments."
        actions={
          canPost ? (
            <Button size="sm" className="h-8 gap-1.5" onClick={() => setOpen(true)}>
              <Plus className="h-3.5 w-3.5" />
              Upsert rate
            </Button>
          ) : null
        }
      />

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : null}


      {!error ? (
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          <DataTable
            table={table}
            paginated
            emptyMessage="No exchange rates yet — add USD→ETB (and other) rates before accepting foreign-currency payments."
          />
        </div>
      ) : null}

      <p className="text-sm text-muted-foreground">
        <Link href="/finance/accounting" className="text-primary underline-offset-4 hover:underline">
          Accounting overview
        </Link>
        {" · "}
        <Link
          href="/workflow/commissions"
          className="text-primary underline-offset-4 hover:underline"
        >
          Commissions
        </Link>
      </p>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent className="w-[400px] sm:max-w-[400px] flex flex-col gap-4 px-6">
          <SheetHeader>
            <SheetTitle>Upsert exchange rate</SheetTitle>
            <SheetDescription>
              Creates or updates the rate for the same from/to/effective date.
            </SheetDescription>
          </SheetHeader>
          <div className="space-y-3">
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
            className="mt-auto bg-green-800 hover:bg-green-900 text-white"
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
