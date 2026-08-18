"use client";

import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { botApi, useBotDeliveries, useBotStatus } from "@/lib/api/bot";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";
import { BotLinkCard } from "@/components/bot/bot-link-card";

export default function BotAdminPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission("bot.configure") || hasPermission("system.admin");
  const canUseBot = hasPermission("bot.use") || canManage;
  const canViewDeliveries =
    hasPermission("notification.configure") || hasPermission("system.admin");

  const { data: status, error, mutate, isLoading } = useBotStatus(canManage);
  const { data: deliveries, error: deliveriesError, mutate: mutateDeliveries } =
    useBotDeliveries(canViewDeliveries);

  if (!canManage) {
    return <AccessDenied resource="bot & notifications" />;
  }

  const onTest = async () => {
    try {
      const result = await botApi.testConnection();
      if (result?.isSuccess) {
        toast.success("Telegram connection succeeded");
        mutate();
      } else {
        toast.error(result?.error || "Telegram connection failed");
      }
    } catch {
      toast.error("Telegram connection failed");
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Bot & notifications"
        description="Telegram connection status, delivery activity, and deferred WhatsApp setup."
      />

      <PageAlert
        variant="info"
        title="Telegram"
        description="The bot token is managed on the server."
      />

      {error ? <LoadError message={error.message} onRetry={() => mutate()} /> : null}

      <div className="rounded-lg border bg-card p-4 shadow-sm space-y-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Telegram</h2>
            <p className="text-sm text-muted-foreground">
              Connection health and runtime polling status.
            </p>
          </div>
          <Button onClick={onTest} className="bg-green-800 hover:bg-green-900 text-white">
            Test connection
          </Button>
        </div>

        <dl className="grid grid-cols-1 gap-3 md:grid-cols-2">
          <StatusRow label="Configured" value={status?.configured ? "Yes" : "No"} loading={isLoading} />
          <StatusRow label="Enabled" value={status?.enabled ? "Yes" : "No"} loading={isLoading} />
          <StatusRow
            label="Polling"
            value={status?.pollingEnabled ? "Enabled" : "Disabled"}
            loading={isLoading}
          />
          <StatusRow
            label="Connection"
            value={status?.isConnected ? "Connected" : "Disconnected"}
            loading={isLoading}
          />
          <StatusRow label="Bot username" value={status?.botUsername || "—"} loading={isLoading} />
          <StatusRow
            label="Last connected"
            value={status?.lastConnectedAt ? new Date(status.lastConnectedAt).toLocaleString() : "—"}
            loading={isLoading}
          />
        </dl>

        {status?.lastError ? (
          <PageAlert variant="error" title="Last error" description={status.lastError} />
        ) : null}
      </div>

      {canUseBot ? <BotLinkCard /> : null}

      <div className="rounded-lg border bg-card p-4 shadow-sm space-y-4">
        <div>
          <h2 className="text-lg font-semibold">WhatsApp</h2>
          <p className="text-sm text-muted-foreground">Deferred to a later batch.</p>
        </div>
        <PageAlert
          variant="info"
          title="Not available yet"
          description="WhatsApp messaging is not enabled."
        />
      </div>

      <div className="rounded-lg border bg-card p-4 shadow-sm space-y-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Recent deliveries</h2>
            <p className="text-sm text-muted-foreground">
              Latest bot push attempts recorded by the platform.
            </p>
          </div>
          <Button variant="outline" onClick={() => mutateDeliveries()}>
            Refresh
          </Button>
        </div>

        {!canViewDeliveries ? (
          <p className="text-sm text-muted-foreground">
            You do not have permission to view delivery logs.
          </p>
        ) : deliveriesError ? (
          <LoadError message={deliveriesError.message} onRetry={() => mutateDeliveries()} />
        ) : deliveries?.items?.length ? (
          <div className="space-y-3">
            {deliveries.items.map((item) => (
              <div key={item.id} className="rounded-md border p-3 text-sm">
                <div className="flex items-center justify-between gap-3">
                  <div className="font-medium">{item.eventType}</div>
                  <div className="text-muted-foreground">{item.status}</div>
                </div>
                <p className="mt-1 text-muted-foreground">{item.payloadSummary}</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No delivery records yet.</p>
        )}
      </div>
    </div>
  );
}

function StatusRow({
  label,
  value,
  loading,
}: {
  label: string;
  value: string;
  loading?: boolean;
}) {
  return (
    <div className="rounded-md border p-3">
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm font-medium">{loading ? "Loading…" : value}</dd>
    </div>
  );
}
