"use client";

import Link from "next/link";
import { AccessDenied, PageAlert } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { useAccounts } from "@/lib/api/accounting";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

export default function AccountingPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("accounting.read") || hasPermission("system.admin");
  const { accounts, isLoading, error } = useAccounts(true);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canRead) {
    return <AccessDenied resource="accounting" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Accounting"
        description="Chart of accounts, commissions and exchange rates."
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild size="sm" variant="outline" className="h-8">
              <Link href="/finance/rates">Exchange rates</Link>
            </Button>
            <Button asChild size="sm" variant="outline" className="h-8">
              <Link href="/workflow/commissions">Commissions</Link>
            </Button>
          </div>
        }
      />

      <PageAlert
        variant="info"
        title="Payment journals"
        description="Each payment posts to cash and revenue."
      />

      {error ? (
        <PageAlert
          variant="error"
          title="Could not load accounts"
          description={error instanceof Error ? error.message : "Request failed"}
        />
      ) : null}

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <h2 className="mb-3 text-lg font-medium">Chart of accounts</h2>
        {isLoading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : accounts.length === 0 ? (
          <p className="text-sm text-muted-foreground">No accounts seeded yet.</p>
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
                    {a.isSystem ? (
                      <span className="ml-2 text-xs text-muted-foreground">system</span>
                    ) : null}
                  </TableCell>
                  <TableCell>{a.type}</TableCell>
                  <TableCell>{a.currency}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        <p className="mt-3 text-xs text-muted-foreground">
          Seeded defaults include Cash/Bank (1100) and Commission Revenue (4100).
        </p>
      </div>
    </div>
  );
}
