"use client";

import { use } from "react";
import Link from "next/link";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { formatEtb } from "@/lib/api/commissions";
import { useJournalEntry } from "@/lib/api/accounting";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { ArrowLeft, Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

export default function JournalDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("accounting.read") || hasPermission("system.admin");
  const { journal, isLoading, error, mutate } = useJournalEntry(canView ? id : undefined);

  if (permsLoading || isLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="Journals" />;

  if (error) {
    return (
      <div className="p-6">
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      </div>
    );
  }

  if (!journal) {
    return (
      <div className="p-6">
        <PageAlert variant="info" title="Not found" description="Journal entry not found." />
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 p-6">
      <Link
        href="/finance/accounting"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to accounting
      </Link>

      <PageHeader
        title={journal.entryNumber}
        description={<>{journal.description} · {new Date(journal.postedAt).toLocaleString()}</>}
      />

      <div className="overflow-x-auto rounded-lg border bg-card shadow-sm">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Account</TableHead>
              <TableHead className="text-right">Debit</TableHead>
              <TableHead className="text-right">Credit</TableHead>
              <TableHead>Memo</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {journal.lines.map((l) => (
              <TableRow key={l.id}>
                <TableCell>
                  <span className="font-mono text-xs">{l.accountCode}</span> {l.accountName}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {l.debit > 0 ? formatEtb(l.debit) : "—"}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {l.credit > 0 ? formatEtb(l.credit) : "—"}
                </TableCell>
                <TableCell className="text-muted-foreground">{l.memo || "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
          <tfoot>
            <TableRow className="bg-muted/20">
              <TableCell>Totals</TableCell>
              <TableCell className="text-right tabular-nums">{formatEtb(journal.totalDebit)}</TableCell>
              <TableCell className="text-right tabular-nums">{formatEtb(journal.totalCredit)}</TableCell>
              <TableCell />
            </TableRow>
          </tfoot>
        </Table>
      </div>
    </div>
  );
}
