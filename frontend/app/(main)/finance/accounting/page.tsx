"use client";

import { useState } from "react";
import Link from "next/link";
import { Loader2 } from "lucide-react";
import { AccessDenied, PageAlert } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  useAccounts,
  useBalanceSheet,
  useJournalEntries,
  useProfitAndLoss,
  useTrialBalance,
  type StatementLine,
} from "@/lib/api/accounting";
import { formatEtb } from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { cn } from "@/lib/utils";

const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => today().slice(0, 8) + "01";

const SOURCE_LABELS: Record<string, string> = {
  CommissionFees: "Fees charged",
  CommissionPayment: "Payment",
  CommissionPaymentVoid: "Payment void",
  DisputeAdjustment: "Dispute write-off",
  Expense: "Expense",
  ExpenseVoid: "Expense void",
  LegacyPaymentReclass: "Reclassification",
};
const sourceLabel = (s: string) => SOURCE_LABELS[s] ?? s;

function ReportError({ error }: { error: unknown }) {
  return (
    <PageAlert
      variant="error"
      title="Could not load"
      description={error instanceof Error ? error.message : "Request failed"}
    />
  );
}

function Loading() {
  return (
    <p className="flex items-center text-sm text-muted-foreground">
      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      Loading…
    </p>
  );
}

function DateField({ id, label, value, onChange }: { id: string; label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div className="flex items-center gap-1.5">
      <Label htmlFor={id} className="text-xs text-muted-foreground">
        {label}
      </Label>
      <Input
        id={id}
        type="date"
        className="h-8 w-[150px]"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  );
}

/**
 * The figure a statement is read for, before the statement itself.
 *
 * Somebody opening Profit and loss wants the number at the bottom; they read the lines to find
 * out how it got there. It was only at the bottom, under however many accounts had been posted
 * to, so the page opened on the detail and buried the answer.
 */
function Stat({ label, value, tone }: { label: string; value: number; tone?: string }) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className={cn("mt-1 text-2xl font-semibold tabular-nums", tone)}>{formatEtb(value)}</p>
    </div>
  );
}

const money = (n: number) => <span className="tabular-nums">{formatEtb(n)}</span>;

export default function AccountingPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = hasPermission("accounting.read") || hasPermission("system.admin");

  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }
  if (!canRead) return <AccessDenied resource="accounting" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Accounting" />

      <Tabs defaultValue="pl" className="flex flex-col gap-4">
        {/* The statement to read and the period to read it over, together: they are one
            question, and the dates floating above the tabs read as a setting for the page
            rather than as half of what is being asked. */}
        <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-card p-3 shadow-sm">
        <TabsList className="flex-wrap">
          <TabsTrigger value="pl">Profit and loss</TabsTrigger>
          <TabsTrigger value="bs">Balance sheet</TabsTrigger>
          <TabsTrigger value="tb">Trial balance</TabsTrigger>
          <TabsTrigger value="journal">Journal</TabsTrigger>
          <TabsTrigger value="accounts">Chart of accounts</TabsTrigger>
        </TabsList>
          <div className="ml-auto flex flex-wrap items-center gap-3">
            <DateField id="acc-from" label="From" value={from} onChange={setFrom} />
            <DateField id="acc-to" label="To (and as of)" value={to} onChange={setTo} />
          </div>
        </div>
        <TabsContent value="pl">
          <ProfitAndLossTab from={from} to={to} />
        </TabsContent>
        <TabsContent value="bs">
          <BalanceSheetTab asOf={to} />
        </TabsContent>
        <TabsContent value="tb">
          <TrialBalanceTab asOf={to} />
        </TabsContent>
        <TabsContent value="journal">
          <JournalTab from={from} to={to} />
        </TabsContent>
        <TabsContent value="accounts">
          <AccountsTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Panel({ children }: { children: React.ReactNode }) {
  return <div className="overflow-x-auto rounded-lg border bg-card p-4 shadow-sm">{children}</div>;
}

function StatementSection({ title, lines, total, totalLabel }: { title: string; lines: StatementLine[]; total: number; totalLabel: string }) {
  return (
    <>
      {/* Muted on a span and not on the cell: the ET theme blockifies a muted cell, which
          drops it out of its own row. */}
      <TableRow className="bg-muted/40 hover:bg-muted/40">
        <TableCell colSpan={2} className="py-2">
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {title}
          </span>
        </TableCell>
      </TableRow>
      {lines.length === 0 ? (
        <TableRow className="hover:bg-transparent">
          <TableCell colSpan={2}>
            <span className="text-sm text-muted-foreground">Nothing posted.</span>
          </TableCell>
        </TableRow>
      ) : (
        lines.map((l) => (
          <TableRow key={l.code}>
            <TableCell>
              <span className="mr-2 font-mono text-xs text-muted-foreground">{l.code}</span>
              {l.name}
            </TableCell>
            <TableCell className="text-right">{money(l.amount)}</TableCell>
          </TableRow>
        ))
      )}
      <TableRow className="border-t-2 font-medium hover:bg-transparent">
        <TableCell>{totalLabel}</TableCell>
        <TableCell className="text-right">{money(total)}</TableCell>
      </TableRow>
    </>
  );
}

function ProfitAndLossTab({ from, to }: { from: string; to: string }) {
  const { data, error, isLoading } = useProfitAndLoss(from, to);
  if (error) return <ReportError error={error} />;
  if (isLoading || !data) return <Loading />;
  const profit = data.netProfit >= 0;
  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Revenue" value={data.totalRevenue} />
        <Stat label="Expenses" value={data.totalExpenses} />
        <Stat
          label={profit ? "Net profit" : "Net loss"}
          value={data.netProfit}
          tone={profit ? "text-green-800 dark:text-green-500" : "text-rose-600 dark:text-rose-400"}
        />
      </div>
    <Panel>
      <p className="mb-3 text-sm text-muted-foreground">
        Partner share, medical and ticket money is owed onward, so it is not income here.
      </p>
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            <TableHead>Account</TableHead>
            <TableHead className="text-right">Amount</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <StatementSection title="Revenue" lines={data.revenue} total={data.totalRevenue} totalLabel="Total revenue" />
          <StatementSection title="Expenses" lines={data.expenses} total={data.totalExpenses} totalLabel="Total expenses" />
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell className="font-semibold">{profit ? "Net profit" : "Net loss"}</TableCell>
            <TableCell
              className={cn(
                "text-right font-semibold",
                profit ? "text-green-800 dark:text-green-500" : "text-rose-600 dark:text-rose-400"
              )}
            >
              {money(data.netProfit)}
            </TableCell>
          </TableRow>
        </TableFooter>
      </Table>
    </Panel>
    </div>
  );
}

function BalanceSheetTab({ asOf }: { asOf: string }) {
  const { data, error, isLoading } = useBalanceSheet(asOf);
  if (error) return <ReportError error={error} />;
  if (isLoading || !data) return <Loading />;
  const balanced = Math.abs(data.totalAssets - data.totalLiabilitiesAndEquity) < 0.005;
  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Assets" value={data.totalAssets} />
        <Stat label="Liabilities" value={data.totalLiabilities} />
        <Stat label="Equity" value={data.totalEquity} />
      </div>
    <Panel>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <p className="text-sm text-muted-foreground">As of {data.asOf}.</p>
        <span
          className={cn(
            "rounded-full px-2 py-0.5 text-[11px] font-medium",
            balanced
              ? "bg-emerald-500/15 text-emerald-700 dark:text-emerald-400"
              : "bg-rose-500/15 text-rose-700 dark:text-rose-400"
          )}
        >
          {balanced ? "Balanced" : "Does not balance"}
        </span>
      </div>
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            <TableHead>Account</TableHead>
            <TableHead className="text-right">Amount</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <StatementSection title="Assets" lines={data.assets} total={data.totalAssets} totalLabel="Total assets" />
          <StatementSection
            title="Liabilities"
            lines={data.liabilities}
            total={data.totalLiabilities}
            totalLabel="Total liabilities"
          />
          <StatementSection
            title="Equity"
            lines={[...data.equity, { code: "", name: "Retained earnings (profit to date)", amount: data.retainedEarnings }]}
            total={data.totalEquity}
            totalLabel="Total equity"
          />
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell className="font-semibold">Total liabilities and equity</TableCell>
            <TableCell className="text-right font-semibold">{money(data.totalLiabilitiesAndEquity)}</TableCell>
          </TableRow>
        </TableFooter>
      </Table>
      {!balanced ? (
        <p className="mt-3 text-sm text-rose-700">Assets and liabilities plus equity do not agree.</p>
      ) : null}
    </Panel>
    </div>
  );
}

function TrialBalanceTab({ asOf }: { asOf: string }) {
  const { data, error, isLoading } = useTrialBalance(asOf);
  if (error) return <ReportError error={error} />;
  if (isLoading || !data) return <Loading />;
  const rows = data.rows.filter((r) => r.debit !== 0 || r.credit !== 0);
  return (
    <Panel>
      <p className="mb-2 text-sm text-muted-foreground">Account balances as of {data.asOf}.</p>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Code</TableHead>
            <TableHead>Account</TableHead>
            <TableHead>Type</TableHead>
            <TableHead className="text-right">Debit</TableHead>
            <TableHead className="text-right">Credit</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={5}>
                <span className="text-sm text-muted-foreground">Nothing posted yet.</span>
              </TableCell>
            </TableRow>
          ) : (
            rows.map((r) => (
              <TableRow key={r.accountId}>
                <TableCell className="font-mono text-xs">{r.code}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.type}</TableCell>
                <TableCell className="text-right">{r.debit ? money(r.debit) : ""}</TableCell>
                <TableCell className="text-right">{r.credit ? money(r.credit) : ""}</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell colSpan={3} className="font-semibold">
              Total
            </TableCell>
            <TableCell className="text-right font-semibold">{money(data.totalDebit)}</TableCell>
            <TableCell className="text-right font-semibold">{money(data.totalCredit)}</TableCell>
          </TableRow>
        </TableFooter>
      </Table>
    </Panel>
  );
}

const PAGE_SIZE = 50;

function JournalTab({ from, to }: { from: string; to: string }) {
  const [page, setPage] = useState(1);
  const [sourceType, setSourceType] = useState("all");
  const [search, setSearch] = useState("");
  const { data, error, isLoading } = useJournalEntries({
    from,
    to,
    sourceType: sourceType === "all" ? undefined : sourceType,
    search: search.trim() || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const pages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <Panel>
      <div className="mb-3 flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="journal-search">Search</Label>
          <Input
            id="journal-search"
            placeholder="Entry number or description"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>
        <div className="space-y-1.5">
          <Label>Kind</Label>
          <Select
            value={sourceType}
            onValueChange={(v) => {
              setSourceType(v);
              setPage(1);
            }}
          >
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All entries</SelectItem>
              {Object.entries(SOURCE_LABELS).map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      {error ? (
        <ReportError error={error} />
      ) : isLoading || !data ? (
        <Loading />
      ) : data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No journal entries in this period.</p>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Entry</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Kind</TableHead>
                <TableHead>Description</TableHead>
                <TableHead className="text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((j) => (
                <TableRow key={j.id}>
                  <TableCell className="whitespace-nowrap font-mono text-xs">
                    <Link href={`/finance/journals/${j.id}`} className="text-primary underline-offset-4 hover:underline">
                      {j.entryNumber}
                    </Link>
                  </TableCell>
                  <TableCell className="whitespace-nowrap">{new Date(j.postedAt).toLocaleDateString()}</TableCell>
                  <TableCell className="whitespace-nowrap">{sourceLabel(j.sourceType)}</TableCell>
                  <TableCell>{j.description}</TableCell>
                  <TableCell className="text-right">{money(j.amount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {data.totalCount} entries · page {page} of {pages}
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </Panel>
  );
}

function AccountsTab() {
  const { accounts, isLoading, error } = useAccounts(true);
  if (error) return <ReportError error={error} />;
  if (isLoading) return <Loading />;
  return (
    <Panel>
      {accounts.length === 0 ? (
        <p className="text-sm text-muted-foreground">No accounts yet.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Currency</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {accounts.map((a) => (
              <TableRow key={a.id}>
                <TableCell className="font-mono text-xs">{a.code}</TableCell>
                <TableCell>
                  {a.name}
                  {a.isSystem ? <span className="ml-2 text-xs text-muted-foreground">system</span> : null}
                </TableCell>
                <TableCell>{a.type}</TableCell>
                <TableCell>{a.currency}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Panel>
  );
}
