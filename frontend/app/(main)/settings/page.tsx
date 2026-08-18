"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { botApi, useBotStatus } from "@/lib/api/bot";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";

export default function SettingsPage() {
  const { hasPermission } = usePermissions();
  const canAdmin = hasPermission("system.admin");
  const canUseBot = hasPermission("bot.use") || canAdmin;

  const [agencyDisplayName, setAgencyDisplayName] = useState("");
  const [saving, setSaving] = useState(false);
  const [linking, setLinking] = useState(false);
  const [linkCode, setLinkCode] = useState<string | null>(null);
  const { data: botStatus, error: botStatusError, mutate: mutateBotStatus } =
    useBotStatus(canUseBot);

  if (!canAdmin && !canUseBot) {
    return <AccessDenied resource="settings" />;
  }

  const onSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const res = await fetch("/api/proxy/settings/tenant", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ displayName: agencyDisplayName }),
      });
      const body = await res.json().catch(() => ({}));
      if (res.ok) {
        toast.success("Settings saved");
      } else {
        toast.error(
          body?.error ||
            "Tenant settings API is not available yet. Preferences will persist when the backend ships."
        );
      }
    } catch {
      toast.error(
        "Tenant settings API is not available yet. Preferences will persist when the backend ships."
      );
    }
    setSaving(false);
  };

  const onGenerateLinkCode = async () => {
    setLinking(true);
    try {
      const result = await botApi.createLinkCode();
      if (result?.isSuccess) {
        setLinkCode(result.data?.code ?? null);
        toast.success("Bot link code created");
        mutateBotStatus();
      } else {
        toast.error(result?.error || "Could not create bot link code");
      }
    } catch {
      toast.error("Could not create bot link code");
    } finally {
      setLinking(false);
    }
  };

  const onUnlink = async () => {
    setLinking(true);
    try {
      const result = await botApi.unlink();
      if (result?.isSuccess) {
        setLinkCode(null);
        toast.success("Bot unlinked");
        mutateBotStatus();
      } else {
        toast.error(result?.error || "Could not unlink bot");
      }
    } catch {
      toast.error("Could not unlink bot");
    } finally {
      setLinking(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 max-w-xl">
      <PageHeader
        title="Settings"
        description="Agency preferences and system options"
      />

      {canAdmin ? (
        <>
          <PageAlert
            variant="info"
            title="Account settings"
            description="Change your password from your profile."
          />

          <form
            onSubmit={onSave}
            className="rounded-lg border bg-card p-4 shadow-sm space-y-4"
          >
            <div className="space-y-1.5">
              <Label htmlFor="agency-name">Agency display name</Label>
              <Input
                id="agency-name"
                value={agencyDisplayName}
                onChange={(e) => setAgencyDisplayName(e.target.value)}
                placeholder="Your agency name"
              />
            </div>
            <Button
              type="submit"
              disabled={saving}
              className="bg-green-800 hover:bg-green-900"
            >
              {saving ? "Saving…" : "Save"}
            </Button>
          </form>
        </>
      ) : null}

      {canUseBot ? (
        <div className="rounded-lg border bg-card p-4 shadow-sm space-y-4">
          <div>
            <h2 className="text-lg font-semibold">Telegram bot</h2>
            <p className="text-sm text-muted-foreground">
              Link your Telegram chat to receive personal updates and candidate notifications.
            </p>
          </div>

          {botStatusError ? (
            <LoadError message={botStatusError.message} onRetry={() => mutateBotStatus()} />
          ) : null}

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-md border p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">Configured</div>
              <div className="mt-1 text-sm font-medium">{botStatus?.configured ? "Yes" : "No"}</div>
            </div>
            <div className="rounded-md border p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">Bot username</div>
              <div className="mt-1 text-sm font-medium">{botStatus?.botUsername || "—"}</div>
            </div>
          </div>

          <PageAlert
            variant="info"
            title="How linking works"
            description="Generate a code here, then send it to the bot as /link CODE."
          />

          {linkCode ? (
            <div className="rounded-md border border-dashed p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">One-time code</div>
              <div className="mt-1 text-2xl font-semibold tracking-widest">{linkCode}</div>
            </div>
          ) : null}

          <div className="flex flex-wrap gap-3">
            <Button
              type="button"
              onClick={onGenerateLinkCode}
              disabled={linking}
              className="bg-green-800 hover:bg-green-900"
            >
              {linking ? "Working…" : "Generate link code"}
            </Button>
            <Button type="button" variant="outline" onClick={onUnlink} disabled={linking}>
              Unlink bot
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
