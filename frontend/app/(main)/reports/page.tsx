"use client";

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useReportCatalog } from "@/lib/api/reports";
import { ReportView } from "@/components/reports/report-view";
import { PageHeader } from "@/components/ui/page-header";
import { cn } from "@/lib/utils";

export default function ReportsPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("report.view") || hasPermission("system.admin");
  const canExport =
    hasPermission("report.export") || hasPermission("system.admin");

  const { data: catalog, error, isLoading, mutate } = useReportCatalog(
    !permsLoading && canView
  );
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    if (!selected && catalog && catalog.length > 0) {
      setSelected(catalog[0].key);
    }
  }, [catalog, selected]);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="reports" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Reports"
        description="Operational and financial reports with Excel & PDF export"
      />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="grid gap-6 lg:grid-cols-4">
        {/* Catalog */}
        <aside className="space-y-2 lg:col-span-1">
          {isLoading && (
            <div className="flex items-center text-sm text-muted-foreground">
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Loading…
            </div>
          )}
          {(catalog ?? []).map((item) => (
            <button
              key={item.key}
              onClick={() => setSelected(item.key)}
              className={cn(
                "w-full rounded-lg border p-3 text-left transition hover:border-primary/40 hover:shadow-sm",
                selected === item.key
                  ? "border-primary/60 bg-primary/5 shadow-sm"
                  : "bg-card"
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-medium">{item.name}</span>
                <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                  {item.category}
                </span>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {item.description}
              </p>
            </button>
          ))}
        </aside>

        {/* Selected report */}
        <section className="lg:col-span-3">
          {selected ? (
            <ReportView reportKey={selected} canExport={canExport} />
          ) : (
            !isLoading && (
              <div className="flex h-64 items-center justify-center rounded-xl border bg-card text-sm text-muted-foreground">
                Select a report to view.
              </div>
            )
          )}
        </section>
      </div>
    </div>
  );
}
