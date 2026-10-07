"use client";

import Link from "next/link";
import { useMemo } from "react";
import { Building2, Banknote, Receipt, AlertTriangle } from "lucide-react";
import { LoadError } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { StatTile } from "@/components/ui/stat-tile";
import { StatusBadge } from "@/components/ui/status-badge";
import { useSubscriptions } from "@/lib/api/subscriptions";
import { money } from "@/lib/billing/format";

/**
 * What the person running the platform needs to see.
 *
 * The agency dashboard counts candidates, stages and commissions — none of which a platform
 * operator owns, and all of which are empty until they switch into an agency. This counts the
 * things they do own: who is signed up, what is being billed, and who is behind.
 *
 * Built from the subscriptions list the Subscriptions page already reads, so the figures here and
 * there cannot drift apart, and no new endpoint had to exist for it.
 */
export function PlatformDashboard() {
  const { rows, error, isLoading, mutate } = useSubscriptions(true);

  const totals = useMemo(() => {
    const currency = rows.find((r) => r.subscriptionCurrency)?.subscriptionCurrency ?? "ETB";
    return {
      currency,
      agencies: rows.length,
      suspended: rows.filter((r) => !r.hasAccess).length,
      outstanding: rows.reduce((sum, r) => sum + r.outstanding, 0),
      overdue: rows.filter((r) => r.notice === "Overdue").length,
      dueSoon: rows.filter((r) => r.notice === "DueSoon" || r.notice === "DueToday").length,
      recurring: rows.reduce(
        (sum, r) =>
          sum + (r.cycle === "Yearly" ? r.subscriptionAmount / 12 : r.subscriptionAmount),
        0,
      ),
    };
  }, [rows]);

  // Suspended first — they cannot use the system at all — then overdue, then due soon.
  const needsAttention = useMemo(
    () =>
      rows
        .filter((r) => !r.hasAccess || r.notice === "Overdue" || r.notice === "DueSoon" || r.notice === "DueToday")
        .sort((a, b) => Number(a.hasAccess) - Number(b.hasAccess)),
    [rows],
  );

  const attentionCount = totals.overdue + totals.dueSoon;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Dashboard"
        actions={
          <Link
            href="/tenants"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            Agencies →
          </Link>
        }
      />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatTile
          label="Agencies"
          value={isLoading ? "—" : String(totals.agencies)}
          detail={
            totals.agencies === 0
              ? "None yet"
              : totals.suspended === 0
                ? "All have access"
                : `${totals.suspended} suspended`
          }
          icon={Building2}
          tone={totals.suspended > 0 ? "warn" : "plain"}
          href="/tenants"
          isLoading={isLoading}
        />
        <StatTile
          label="Monthly recurring"
          value={isLoading ? "—" : money(Math.round(totals.recurring), totals.currency)}
          detail="Yearly plans counted per month"
          icon={Banknote}
          href="/subscriptions"
          isLoading={isLoading}
        />
        <StatTile
          label="Outstanding"
          value={isLoading ? "—" : money(Math.round(totals.outstanding), totals.currency)}
          detail={totals.outstanding === 0 ? "Nothing unpaid" : "Across all agencies"}
          icon={Receipt}
          tone={totals.outstanding > 0 ? "warn" : "plain"}
          href="/subscriptions"
          isLoading={isLoading}
        />
        <StatTile
          label="Needs attention"
          value={isLoading ? "—" : String(attentionCount)}
          detail={
            attentionCount === 0
              ? "Nothing due"
              : `${totals.overdue} overdue, ${totals.dueSoon} due soon`
          }
          icon={AlertTriangle}
          tone={totals.overdue > 0 ? "bad" : attentionCount > 0 ? "warn" : "plain"}
          href="/subscriptions"
          isLoading={isLoading}
        />
      </div>

      {!isLoading && totals.agencies === 0 ? (
        <div className="rounded-lg border border-dashed p-10 text-center">
          <p className="text-sm font-medium">No agencies yet</p>
          <p className="mt-1 text-sm text-muted-foreground">
            <Link href="/tenants" className="text-primary underline-offset-4 hover:underline">
              Create the first one
            </Link>
            , then pick it in the switcher above to work inside it.
          </p>
        </div>
      ) : (
        <div className="rounded-lg border bg-card shadow-sm">
          <div className="border-b px-4 py-3">
            <h2 className="text-sm font-semibold">Needs attention</h2>
          </div>
          {needsAttention.length === 0 ? (
            <p className="p-10 text-center text-sm text-muted-foreground">
              Every agency is paid up and has access.
            </p>
          ) : (
            needsAttention.map((row) => (
              <Link
                key={row.id}
                href={`/subscriptions/${row.id}`}
                className="flex items-center gap-3 border-b px-4 py-3 text-sm transition last:border-0 hover:bg-muted/40"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">{row.name}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {row.hasAccess ? "Active" : "Suspended — staff cannot sign in"}
                    {row.outstanding > 0
                      ? ` · ${money(row.outstanding, row.subscriptionCurrency || totals.currency)} outstanding`
                      : ""}
                  </p>
                </div>
                <StatusBadge value={row.hasAccess ? row.notice : "Suspended"} />
              </Link>
            ))
          )}
        </div>
      )}
    </div>
  );
}
