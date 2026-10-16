"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { Ban, BookOpen, Loader2, MoreHorizontal, Plus } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import {
  EXPENSE_CATEGORIES,
  accountingApi,
  useExpenses,
  type ExpenseRow,
} from "@/lib/api/accounting";
import { formatEtb } from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";

const METHODS = [
  { value: "Cash", label: "Cash" },
  { value: "BankTransfer", label: "Bank transfer" },
  { value: "MobileMoney", label: "Mobile money" },
  { value: "Other", label: "Other" },
];

const categoryLabel = (value: string) =>
  EXPENSE_CATEGORIES.find((c) => c.value === value)?.label ?? value;

const methodLabel = (value: string) => METHODS.find((m) => m.value === value)?.label ?? value;

/** A row as the table reads it, with the words its funnels compare against. */
type ExpenseTableRow = ExpenseRow & { categoryName: string; methodName: string; state: string };

const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => today().slice(0, 8) + "01";

export default function ExpensesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("accounting.read") || hasPermission("system.admin");
  const canPost = hasPermission("accounting.post") || hasPermission("system.admin");

  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const { expenses, isLoading, error, mutate } = useExpenses({ from, to });

  const [open, setOpen] = useState(false);
  const [voiding, setVoiding] = useState<ExpenseRow | null>(null);

  const totals = useMemo(() => {
    const live = expenses.filter((e) => !e.voidedAt);
    const payouts = live
      .filter((e) => EXPENSE_CATEGORIES.find((c) => c.value === e.category)?.payout)
      .reduce((sum, e) => sum + e.amountEtb, 0);
    const all = live.reduce((sum, e) => sum + e.amountEtb, 0);
    return { all, payouts, operating: all - payouts };
  }, [expenses]);

  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([]);
  // The period's expenses arrive whole, so the funnels are answered here; the dates are what
  // the server is asked for.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const rows = useMemo<ExpenseTableRow[]>(
    () =>
      applyColumnFilters(
        expenses.map((e) => ({
          ...e,
          categoryName: categoryLabel(e.category),
          methodName: methodLabel(e.method),
          state: e.voidedAt ? "Void" : "Posted",
        })),
        filters,
        {
          expenseDate: "date",
          categoryName: "text",
          description: "text",
          payee: "text",
          methodName: "text",
          amount: "number",
          amountEtb: "number",
          state: "text",
        },
      ),
    [expenses, filters],
  );

  const columns: ColumnDef<ExpenseTableRow>[] = useMemo(
    () => [
      indexColumn<ExpenseTableRow>(),
      {
        accessorKey: "expenseDate",
        meta: { filter: { field: "expenseDate", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Date" />,
        cell: ({ row }) => <span className="whitespace-nowrap">{row.original.expenseDate}</span>,
      },
      {
        accessorKey: "categoryName",
        meta: { filter: { field: "categoryName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Category" />,
        cell: ({ row }) => <span className="whitespace-nowrap">{row.original.categoryName}</span>,
      },
      {
        accessorKey: "description",
        meta: { filter: { field: "description", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Description" />,
      },
      {
        accessorKey: "payee",
        meta: { filter: { field: "payee", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Payee" />,
        cell: ({ row }) => row.original.payee || "—",
      },
      {
        accessorKey: "methodName",
        meta: { filter: { field: "methodName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Method" />,
      },
      {
        accessorKey: "amount",
        meta: { filter: { field: "amount", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Amount" />,
        cell: ({ row }) => (
          <span className={`tabular-nums ${row.original.voidedAt ? "line-through" : ""}`}>
            {row.original.amount.toFixed(2)} {row.original.currency}
          </span>
        ),
      },
      {
        accessorKey: "amountEtb",
        meta: { filter: { field: "amountEtb", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="ETB" />,
        cell: ({ row }) => (
          <span className={`tabular-nums ${row.original.voidedAt ? "line-through" : ""}`}>
            {formatEtb(row.original.amountEtb)}
          </span>
        ),
      },
      {
        accessorKey: "state",
        meta: { filter: { field: "state", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ row }) =>
          row.original.voidedAt ? (
            <span
              className="rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-800"
              title={row.original.voidReason ?? undefined}
            >
              Void
            </span>
          ) : (
            <span className="text-muted-foreground">Posted</span>
          ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => {
          const e = row.original;
          const canVoid = canPost && !e.voidedAt;
          if (!e.journalEntryId && !canVoid) return null;
          return (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {e.journalEntryId && (
                  <DropdownMenuItem onClick={() => router.push(`/finance/journals/${e.journalEntryId}`)}>
                    <BookOpen className="mr-2 h-4 w-4" /> Open journal entry
                  </DropdownMenuItem>
                )}
                {canVoid && (
                  <DropdownMenuItem className="text-destructive" onClick={() => setVoiding(e)}>
                    <Ban className="mr-2 h-4 w-4" /> Void…
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          );
        },
        size: 60,
        enableSorting: false,
      },
    ],
    [canPost, router],
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "expenses", columnIds });

  const table = useReactTable({
    data: rows,
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
    initialState: { pagination: { pageSize: 20 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }
  if (!canView) return <AccessDenied resource="expenses" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Expenses" count={expenses.length} />

      <div className="grid gap-4 sm:grid-cols-3">
        <Tile label="Operating expenses" value={totals.operating} hint="Counts against profit" />
        <Tile label="Payouts" value={totals.payouts} hint="Medical, ticket and partner money passed on" />
        <Tile label="Total paid out" value={totals.all} />
      </div>

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : null}

      <div className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        {/* The period is what the server is asked for, so it sits above the table rather than in
            its toolbar, whose search and funnels narrow the expenses already in hand. */}
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <Label htmlFor="exp-from" className="text-muted-foreground">From</Label>
          <Input
            id="exp-from"
            type="date"
            className="h-8 w-[150px]"
            value={from}
            max={to || undefined}
            onChange={(e) => setFrom(e.target.value)}
          />
          <Label htmlFor="exp-to" className="text-muted-foreground">To</Label>
          <Input
            id="exp-to"
            type="date"
            className="h-8 w-[150px]"
            value={to}
            min={from || undefined}
            onChange={(e) => setTo(e.target.value)}
          />
        </div>
        <DataTable
          layout={layout}
          rowClickOpensActions
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search expenses…"
          paginated
          exportFileName={`expenses-${from}-to-${to}`}
          filters={filters}
          onFiltersChange={setFilters}
          emptyState={
            isLoading ? (
              <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Loading…
              </div>
            ) : undefined
          }
          emptyMessage="No expenses in this period."
          createAction={
            canPost ? (
              <Button
                size="sm"
                className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
                onClick={() => setOpen(true)}
              >
                <Plus className="h-3.5 w-3.5" />
                Record expense
              </Button>
            ) : undefined
          }
        />
      </div>

      <RecordExpenseSheet open={open} onOpenChange={setOpen} onRecorded={() => mutate()} />
      <VoidExpenseDialog expense={voiding} onClose={() => setVoiding(null)} onVoided={() => mutate()} />
    </div>
  );
}

function Tile({ label, value, hint }: { label: string; value: number; hint?: string }) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="text-xl font-semibold tabular-nums">{formatEtb(value)}</p>
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

function RecordExpenseSheet({
  open,
  onOpenChange,
  onRecorded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onRecorded: () => void;
}) {
  const [expenseDate, setExpenseDate] = useState(today);
  const [category, setCategory] = useState("Other");
  const [description, setDescription] = useState("");
  const [payee, setPayee] = useState("");
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("ETB");
  const [method, setMethod] = useState("Cash");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);

  const payout = EXPENSE_CATEGORIES.find((c) => c.value === category)?.payout;

  const submit = async () => {
    const value = Number(amount);
    if (!(value > 0)) {
      toast.error("Amount must be greater than 0");
      return;
    }
    if (!description.trim()) {
      toast.error("Description is required");
      return;
    }
    setBusy(true);
    try {
      await accountingApi.recordExpense({
        expenseDate,
        category,
        description: description.trim(),
        amount: value,
        currency,
        method,
        payee: payee.trim() || undefined,
        reference: reference.trim() || undefined,
      });
      toast.success("Expense recorded and posted");
      setDescription("");
      setPayee("");
      setAmount("");
      setReference("");
      onOpenChange(false);
      onRecorded();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not record the expense");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-[420px] flex-col gap-4 overflow-y-auto sm:max-w-[420px]">
        <SheetHeader>
          <SheetTitle>Record expense</SheetTitle>
          <SheetDescription>
            {payout
              ? "A payout settles money collected for someone else, so it reduces what is owed onward rather than profit."
              : "Posts the expense against cash for the date paid."}
          </SheetDescription>
        </SheetHeader>
        <div className="space-y-3 px-6">
          <div className="space-y-1.5">
            <Label>Date paid</Label>
            <Input type="date" value={expenseDate} onChange={(e) => setExpenseDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Category</Label>
            <Select value={category} onValueChange={setCategory}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {EXPENSE_CATEGORIES.map((c) => (
                  <SelectItem key={c.value} value={c.value}>
                    {c.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Input value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Paid to</Label>
            <Input value={payee} onChange={(e) => setPayee(e.target.value)} />
          </div>
          <div className="grid grid-cols-[1fr_6rem] gap-3">
            <div className="space-y-1.5">
              <Label>Amount</Label>
              <Input
                type="number"
                min={0.01}
                step="0.01"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Currency</Label>
              <Input
                value={currency}
                maxLength={3}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Method</Label>
            <Select value={method} onValueChange={setMethod}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {METHODS.map((m) => (
                  <SelectItem key={m.value} value={m.value}>
                    {m.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Reference</Label>
            <Input value={reference} onChange={(e) => setReference(e.target.value)} />
          </div>
        </div>
        <Button
          className="mx-6 mb-6 mt-auto bg-green-800 text-white hover:bg-green-900"
          disabled={busy}
          onClick={submit}
        >
          {busy ? "Saving…" : "Save"}
        </Button>
      </SheetContent>
    </Sheet>
  );
}

function VoidExpenseDialog({
  expense,
  onClose,
  onVoided,
}: {
  expense: ExpenseRow | null;
  onClose: () => void;
  onVoided: () => void;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!expense) return;
    if (!reason.trim()) {
      toast.error("Say why the expense is being voided");
      return;
    }
    setBusy(true);
    try {
      await accountingApi.voidExpense(expense.id, reason.trim());
      toast.success("Expense voided and reversed in the ledger");
      setReason("");
      onClose();
      onVoided();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not void the expense");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={!!expense} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Void expense</DialogTitle>
          <DialogDescription>
            {expense ? `${expense.description}, ${formatEtb(expense.amountEtb)}. ` : ""}
            The expense stays listed, marked void, and a reversing entry is posted.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-1.5">
          <Label htmlFor="exp-void-reason">Reason</Label>
          <Textarea id="exp-void-reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "Voiding…" : "Void expense"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
