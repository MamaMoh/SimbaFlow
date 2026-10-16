"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { Ban, CheckCircle2, FileText, Loader2, MoreHorizontal, Pencil, Receipt } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { StatTile } from "@/components/ui/stat-tile";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";
import {
  subscriptionApi,
  useSubscriptions,
  type SubscriptionRow,
} from "@/lib/api/subscriptions";
import { money, onDate } from "@/lib/billing/format";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import type { ColumnFilter } from "@/lib/data-table/column-filters";

/** A row as the table reads it, with the plan already in words so its funnel can compare them. */
type SubscriptionTableRow = SubscriptionRow & { plan: string };

/** The plan as the Plan column says it. */
function planLabel(row: SubscriptionRow) {
  return row.subscriptionAmount > 0
    ? `${money(row.subscriptionAmount, row.subscriptionCurrency)} · ${row.cycle.toLowerCase()}`
    : "Not charged";
}

/** Fixed, so the remembered column order is not rebuilt on every render. */
const COLUMN_IDS = ["index", "name", "status", "plan", "nextPaymentDue", "outstanding", "actions"];

/** When the next payment lands, said the way someone would say it. */
function dueLabel(row: SubscriptionRow) {
  if (!row.nextPaymentDue) return "Not scheduled";
  const d = row.daysUntilDue;
  if (d === null) return onDate(row.nextPaymentDue);
  if (d < 0) return `${Math.abs(d)} ${Math.abs(d) === 1 ? "day" : "days"} overdue`;
  if (d === 0) return "Due today";
  return `in ${d} ${d === 1 ? "day" : "days"}`;
}

export default function SubscriptionsPage() {
  // The support desk reads billing; only administrators change it.
  const { isSuperAdmin, isStaff } = usePlatformStaff();
  const { rows, error, isLoading, mutate } = useSubscriptions(isStaff);

  const [editing, setEditing] = useState<SubscriptionRow | null>(null);
  const [invoicing, setInvoicing] = useState<SubscriptionRow | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Read off the rows themselves so the figures always agree with the table beneath them.
  const totals = useMemo(() => {
    const currency = rows.find((r) => r.subscriptionCurrency)?.subscriptionCurrency ?? "ETB";
    return {
      currency,
      agencies: rows.length,
      suspended: rows.filter((r) => !r.hasAccess).length,
      unpaid: rows.reduce((sum, r) => sum + r.outstanding, 0),
      dueSoon: rows.filter((r) => r.notice === "DueSoon" || r.notice === "DueToday").length,
      overdue: rows.filter((r) => r.notice === "Overdue").length,
      recurring: rows.reduce(
        (sum, r) => sum + (r.cycle === "Yearly" ? r.subscriptionAmount / 12 : r.subscriptionAmount),
        0,
      ),
    };
  }, [rows]);

  const [sorting, setSorting] = useState<SortingState>([]);
  // Every agency arrives at once, so the funnels are answered here like the agencies board.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const filtered = useMemo<SubscriptionTableRow[]>(
    () =>
      applyColumnFilters(
        rows.map((r) => ({ ...r, plan: planLabel(r) })),
        filters,
        { name: "text", status: "text", plan: "text", nextPaymentDue: "date", outstanding: "number" },
      ),
    [rows, filters],
  );

  const setStatus = async (row: SubscriptionRow, status: "Active" | "Suspended") => {
    setBusyId(row.id);
    try {
      await subscriptionApi.update(row.id, { status });
      toast.success(
        status === "Active"
          ? `${row.name} can use the system again`
          : `${row.name} is suspended — their staff can no longer sign in to their data`,
      );
      void mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not change the status.");
    } finally {
      setBusyId(null);
    }
  };

  const columns: ColumnDef<SubscriptionTableRow>[] = [
    indexColumn<SubscriptionTableRow>(),
    {
      accessorKey: "name",
      meta: { filter: { field: "name", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Agency" />,
      cell: ({ row }) => <span className="font-medium">{row.original.name}</span>,
    },
    {
      accessorKey: "status",
      meta: { filter: { field: "status", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Access" />,
      cell: ({ row }) => (
        <Badge
          variant={row.original.hasAccess ? "default" : "destructive"}
          className={row.original.hasAccess ? "bg-green-700 hover:bg-green-700" : ""}
        >
          {row.original.status}
        </Badge>
      ),
    },
    {
      accessorKey: "plan",
      meta: { filter: { field: "plan", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Plan" />,
      cell: ({ row }) => <span className="text-muted-foreground">{row.original.plan}</span>,
    },
    {
      accessorKey: "nextPaymentDue",
      meta: { filter: { field: "nextPaymentDue", kind: "date" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Next payment" />,
      cell: ({ row }) => (
        <>
          <span
            className={
              row.original.notice === "Overdue"
                ? "font-medium text-red-700"
                : row.original.notice === "DueSoon" || row.original.notice === "DueToday"
                  ? "font-medium text-amber-700"
                  : "text-muted-foreground"
            }
          >
            {dueLabel(row.original)}
          </span>
          {row.original.nextPaymentDue ? (
            <span className="block text-xs text-muted-foreground">{onDate(row.original.nextPaymentDue)}</span>
          ) : null}
        </>
      ),
    },
    {
      accessorKey: "outstanding",
      meta: { filter: { field: "outstanding", kind: "number" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Unpaid" />,
      cell: ({ row }) =>
        row.original.outstanding > 0 ? (
          <span className="font-medium text-amber-700">{row.original.outstanding}</span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => {
        const r = row.original;
        return (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            {/* Edit and the destructive item together at the top, then a rule, then the rest —
                the shape every row menu in the app uses. The support desk reads billing, so it
                sees History alone. */}
            <DropdownMenuContent align="end">
              {isSuperAdmin && (
                <>
                  <DropdownMenuItem onClick={() => setEditing(r)}>
                    <Pencil className="mr-2 h-4 w-4" /> Edit plan
                  </DropdownMenuItem>
                  {r.hasAccess ? (
                    <DropdownMenuItem
                      className="text-destructive"
                      disabled={busyId === r.id}
                      onClick={() => setStatus(r, "Suspended")}
                    >
                      <Ban className="mr-2 h-4 w-4" /> Suspend
                    </DropdownMenuItem>
                  ) : (
                    <DropdownMenuItem disabled={busyId === r.id} onClick={() => setStatus(r, "Active")}>
                      <CheckCircle2 className="mr-2 h-4 w-4" /> Reactivate
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => setInvoicing(r)}>
                    <Receipt className="mr-2 h-4 w-4" /> Raise invoice
                  </DropdownMenuItem>
                </>
              )}
              <DropdownMenuItem asChild>
                <Link href={`/subscriptions/${r.id}`}>
                  <FileText className="mr-2 h-4 w-4" /> History
                </Link>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        );
      },
      size: 60,
      enableSorting: false,
    },
  ];

  if (!isStaff) return <AccessDenied resource="subscriptions" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Subscriptions" count={rows.length} />

      {error ? (
        <LoadError message="Could not load subscriptions" onRetry={() => mutate()} />
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="Agencies"
          value={String(totals.agencies)}
          detail={
            totals.suspended === 0
              ? "All have access"
              : `${totals.suspended} suspended`
          }
          tone={totals.suspended > 0 ? "warn" : "plain"}
        />
        <StatTile
          label="Monthly recurring"
          value={money(Math.round(totals.recurring), totals.currency)}
          detail="Yearly plans counted per month"
        />
        <StatTile
          label="Unpaid invoices"
          value={String(totals.unpaid)}
          detail={totals.unpaid === 0 ? "Nothing outstanding" : "Across all agencies"}
          tone={totals.unpaid > 0 ? "warn" : "plain"}
        />
        <StatTile
          label="Needs attention"
          value={String(totals.overdue + totals.dueSoon)}
          detail={
            totals.overdue > 0
              ? `${totals.overdue} overdue, ${totals.dueSoon} due soon`
              : totals.dueSoon > 0
                ? `${totals.dueSoon} due soon`
                : "Nothing due"
          }
          tone={totals.overdue > 0 ? "bad" : totals.dueSoon > 0 ? "warn" : "plain"}
        />
      </div>

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <SubscriptionsTable
          data={filtered}
          columns={columns}
          sorting={sorting}
          setSorting={setSorting}
          globalFilter={globalFilter}
          setGlobalFilter={setGlobalFilter}
          filters={filters}
          setFilters={setFilters}
          isLoading={isLoading}
        />
      </div>

      <PlanDialog row={editing} onClose={() => setEditing(null)} onSaved={() => void mutate()} />
      <InvoiceDialog row={invoicing} onClose={() => setInvoicing(null)} onSaved={() => void mutate()} />
    </div>
  );
}

/**
 * The table itself, on its own so that its hooks sit below the page's access check rather than
 * having to run for someone who is about to be turned away.
 */
function SubscriptionsTable({
  data,
  columns,
  sorting,
  setSorting,
  globalFilter,
  setGlobalFilter,
  filters,
  setFilters,
  isLoading,
}: {
  data: SubscriptionTableRow[];
  columns: ColumnDef<SubscriptionTableRow>[];
  sorting: SortingState;
  setSorting: React.Dispatch<React.SetStateAction<SortingState>>;
  globalFilter: string;
  setGlobalFilter: (value: string) => void;
  filters: ColumnFilter[];
  setFilters: (next: ColumnFilter[]) => void;
  isLoading: boolean;
}) {
  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "subscriptions", columnIds: COLUMN_IDS });

  const table = useReactTable({
    data,
    columns,
    state: {
      sorting,
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  return (
    <DataTable
      layout={layout}
      rowClickOpensActions
      table={table}
      enableGlobalFilter
      searchPlaceholder="Search agencies…"
      paginated
      exportFileName="subscriptions"
      filters={filters}
      onFiltersChange={setFilters}
      emptyState={
        isLoading ? (
          <div className="py-10 text-center text-muted-foreground">
            <Loader2 className="mx-auto h-4 w-4 animate-spin" />
          </div>
        ) : undefined
      }
      emptyMessage="No agencies yet."
    />
  );
}

function PlanDialog({
  row,
  onClose,
  onSaved,
}: {
  row: SubscriptionRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("ETB");
  const [cycle, setCycle] = useState("Monthly");
  const [due, setDue] = useState("");
  const [saving, setSaving] = useState(false);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  if (row && loadedFor !== row.id) {
    setLoadedFor(row.id);
    setAmount(row.subscriptionAmount ? String(row.subscriptionAmount) : "");
    setCurrency(row.subscriptionCurrency || "ETB");
    setCycle(row.cycle);
    setDue(row.nextPaymentDue ? row.nextPaymentDue.slice(0, 10) : "");
  }

  const save = async () => {
    if (!row) return;
    setSaving(true);
    try {
      await subscriptionApi.update(row.id, {
        amount: amount ? Number(amount) : 0,
        currency,
        cycle,
        nextPaymentDue: due || null,
      });
      toast.success(`${row.name}'s plan saved`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not save the plan.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!row} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[420px]">
        <DialogHeader>
          <DialogTitle>{row?.name} plan</DialogTitle>
          <DialogDescription>
            What this agency pays and when. Leaving the date empty means nothing is scheduled and
            they see no renewal notice.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="sub-amount">Amount per period</Label>
              <Input
                id="sub-amount"
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="0"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="sub-currency">Currency</Label>
              <Input
                id="sub-currency"
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                maxLength={8}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Billed</Label>
            <Select value={cycle} onValueChange={setCycle}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Yearly">Yearly</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="sub-due">Next payment due</Label>
            <Input id="sub-due" type="date" value={due} onChange={(e) => setDue(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="bg-green-800 hover:bg-green-900">
            {saving ? "Saving…" : "Save plan"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function InvoiceDialog({
  row,
  onClose,
  onSaved,
}: {
  row: SubscriptionRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [start, setStart] = useState("");
  const [cycle, setCycle] = useState("Monthly");
  const [amount, setAmount] = useState("");
  const [dueInDays, setDueInDays] = useState("14");
  const [saving, setSaving] = useState(false);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  if (row && loadedFor !== row.id) {
    setLoadedFor(row.id);
    setStart(row.nextPaymentDue ? row.nextPaymentDue.slice(0, 10) : "");
    setCycle(row.cycle);
    setAmount(row.subscriptionAmount ? String(row.subscriptionAmount) : "");
  }

  const generate = async () => {
    if (!row) return;
    setSaving(true);
    try {
      const res = await subscriptionApi.generateInvoice(row.id, {
        periodStart: start || null,
        cycle,
        amount: amount ? Number(amount) : null,
        dueInDays: Number(dueInDays) || 14,
      });
      toast.success(`${res?.data?.number ?? "Invoice"} raised for ${row.name}`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not raise the invoice.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!row} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[420px]">
        <DialogHeader>
          <DialogTitle>Invoice {row?.name}</DialogTitle>
          <DialogDescription>
            Raises one period&rsquo;s charge. The period runs from the start date to the day before
            the next one begins, so consecutive invoices meet without overlapping.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label>Period</Label>
            <Select value={cycle} onValueChange={setCycle}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Monthly">One month</SelectItem>
                <SelectItem value="Yearly">One year</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="inv-start">Starting</Label>
            <Input id="inv-start" type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="inv-amount">Amount</Label>
              <Input
                id="inv-amount"
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder={row ? String(row.subscriptionAmount) : "0"}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="inv-due">Payable within</Label>
              <Input
                id="inv-due"
                inputMode="numeric"
                value={dueInDays}
                onChange={(e) => setDueInDays(e.target.value)}
              />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={generate} disabled={saving} className="bg-green-800 hover:bg-green-900">
            {saving ? "Raising…" : "Raise invoice"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
