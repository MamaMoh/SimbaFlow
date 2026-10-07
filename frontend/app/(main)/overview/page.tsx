"use client";

import Link from "next/link";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  usePipelineFunnel,
  useDashboardMetrics,
  useDashboardTrends,
} from "@/lib/api/dashboard";
import { useComplianceAlerts, useMyTasks } from "@/lib/api/insights";
import { Loader2 } from "lucide-react";
import { PipelineFunnel } from "@/components/dashboard/pipeline-funnel";
import { StatTiles } from "@/components/dashboard/stat-tiles";
import { TrendChart } from "@/components/dashboard/trend-chart";
import { AlertsStrip } from "@/components/dashboard/alerts-strip";
import { PageHeader } from "@/components/ui/page-header";
import { PlatformDashboard } from "@/components/dashboard/platform-dashboard";
import { useActingTenantId } from "@/lib/tenant/acting-tenant";

export default function OverviewPage() {
  const { hasPermission, isSuperAdmin, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("candidate.read") || hasPermission("system.admin");

  // Same route, two dashboards. A platform administrator with no agency selected is running the
  // platform; counting candidates and pipeline stages at them is counting somebody else's work,
  // and until they pick an agency those numbers are empty anyway.
  const actingTenantId = useActingTenantId();
  const showPlatform = isSuperAdmin && actingTenantId === null;

  // Hold the agency's queries back while that is still unknown, so switching to the platform
  // dashboard does not fire a round of requests nobody will read.
  const enabled = !permsLoading && canRead && actingTenantId !== undefined && !showPlatform;

  const funnel = usePipelineFunnel(enabled);
  const metrics = useDashboardMetrics(enabled);
  const trends = useDashboardTrends(enabled);
  const compliance = useComplianceAlerts(enabled);
  const tasks = useMyTasks(enabled);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) {
    return <AccessDenied resource="the dashboard" />;
  }

  if (showPlatform) return <PlatformDashboard />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Dashboard"
        actions={
          <Link
            href="/reports"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            Open reports →
          </Link>
        }
      />

      {metrics.error && (
        <LoadError
          message={metrics.error.message}
          onRetry={() => metrics.mutate()}
        />
      )}

      <StatTiles metrics={metrics.data} isLoading={metrics.isLoading} />

      <AlertsStrip compliance={compliance.data} tasks={tasks.data} />

      <div className="grid gap-6 lg:grid-cols-5">
        <div className="lg:col-span-3">
          <TrendChart data={trends.data} isLoading={trends.isLoading} />
        </div>
        <div className="lg:col-span-2">
          {funnel.error ? (
            <LoadError
              message={funnel.error.message || "Pipeline funnel unavailable"}
              onRetry={() => funnel.mutate()}
            />
          ) : (
            <PipelineFunnel
              stages={funnel.data?.stages ?? []}
              isLoading={funnel.isLoading}
            />
          )}
        </div>
      </div>
    </div>
  );
}
