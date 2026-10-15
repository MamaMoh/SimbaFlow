"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import {
  botApi,
  useBotLinks,
  useBotNotifications,
  useBotStatus,
  type BotChatLink,
} from "@/lib/api/bot";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { describeLink, lastSeenLabel, linkSubtitle } from "@/lib/bot/chat-links";
import { formatDateTime } from "@/lib/utils";
import { Send, X } from "lucide-react";

/**
 * Personal Telegram linking.
 *
 * Every user links their own account — the code ties a Telegram chat to their SimbaFlow user,
 * which is how the bot knows their agency, role and permissions. An owner cannot generate a code
 * for someone else.
 *
 * Several phones may be linked at once, and each one is listed. It used to be one: linking a
 * second replaced the first silently, so the office handset stopped buzzing with nothing on this
 * screen to say so — the card showed no links at all.
 *
 * Rendered on both Settings and Bot & notifications: staff look for it under the bot page,
 * not under settings.
 */
export function BotLinkCard() {
  const [linking, setLinking] = useState(false);
  const [linkCode, setLinkCode] = useState<string | null>(null);
  const [removing, setRemoving] = useState<BotChatLink | null>(null);
  const [removingAll, setRemovingAll] = useState(false);
  const [busy, setBusy] = useState(false);
  const { data: botStatus, mutate: mutateBotStatus } = useBotStatus(true);
  const { data: links, mutate: mutateLinks } = useBotLinks(true);
  const { data: notificationsOn, mutate: mutateNotifications } = useBotNotifications(true);

  const linked = links ?? [];

  const onToggleNotifications = async (on: boolean) => {
    // Shown as already moved. A switch that waits for a round trip before it moves reads as
    // broken, and the worst case here is that it goes back.
    mutateNotifications(on, false);
    try {
      const result = await botApi.setNotifications(on);
      if (!result?.isSuccess) {
        toast.error(result?.error || "Could not change it");
        mutateNotifications();
        return;
      }
      toast.success(on ? "Telegram messages on" : "Telegram messages off");
    } catch {
      toast.error("Could not change it");
      mutateNotifications();
    }
  };

  const onGenerate = async () => {
    setLinking(true);
    try {
      const result = await botApi.createLinkCode();
      if (result?.isSuccess) {
        setLinkCode(result.data?.code ?? null);
        toast.success("Link code created");
        mutateBotStatus();
      } else {
        toast.error(result?.error || "Could not create a link code");
      }
    } catch {
      toast.error("Could not create a link code");
    } finally {
      setLinking(false);
    }
  };

  const onRemoveOne = async () => {
    if (!removing) return;
    setBusy(true);
    try {
      const result = await botApi.removeLink(removing.id);
      if (result?.isSuccess) {
        toast.success(`${describeLink(removing)} disconnected`);
        setRemoving(null);
        mutateLinks();
      } else {
        toast.error(result?.error || "Could not disconnect it");
      }
    } catch {
      toast.error("Could not disconnect it");
    } finally {
      setBusy(false);
    }
  };

  const onRemoveAll = async () => {
    setBusy(true);
    try {
      const result = await botApi.unlink();
      if (result?.isSuccess) {
        setLinkCode(null);
        toast.success("Telegram unlinked");
        setRemovingAll(false);
        mutateLinks();
        mutateBotStatus();
      } else {
        toast.error(result?.error || "Could not unlink");
      }
    } catch {
      toast.error("Could not unlink");
    } finally {
      setBusy(false);
    }
  };

  const botName = botStatus?.botUsername;

  return (
    <div className="flex h-full flex-col gap-4 rounded-lg border bg-card p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold">My Telegram</h2>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Look up candidates from your phone. More than one phone can be linked — an office
          number and your own both ring.
        </p>
      </div>

      {/* Only where there is something to silence. On an account with no phone linked it is a
          switch over nothing, and the question it asks has not come up yet. */}
      {linked.length > 0 && notificationsOn !== undefined && (
        <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2.5">
          <div className="min-w-0">
            <Label htmlFor="bot-notifications" className="text-sm font-medium">
              Tell me when something happens
            </Label>
            <p className="mt-0.5 text-xs text-muted-foreground">
              A candidate registered, a ticket booked, a departure or an arrival confirmed,
              anyone returned or reported missing, and the morning summary. Looking things up
              in the chat still works either way.
            </p>
          </div>
          <Switch
            id="bot-notifications"
            checked={notificationsOn}
            onCheckedChange={onToggleNotifications}
          />
        </div>
      )}

      {linked.length > 0 ? (
        <ul className="divide-y rounded-md border">
          {linked.map((link) => (
            <li key={link.id} className="flex items-center justify-between gap-3 p-3">
              <div className="flex min-w-0 items-center gap-3">
                <Send className="h-4 w-4 shrink-0 text-muted-foreground" />
                <div className="min-w-0">
                  <div className="truncate text-sm font-medium">{describeLink(link)}</div>
                  <p className="truncate text-xs text-muted-foreground">
                    {linkSubtitle(link) ? `${linkSubtitle(link)} · ` : ""}
                    Linked {formatDateTime(link.linkedAt)} · Last used{" "}
                    {lastSeenLabel(link).toLowerCase()}
                  </p>
                </div>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Disconnect ${describeLink(link)}`}
                className="h-8 w-8 shrink-0 text-destructive"
                onClick={() => setRemoving(link)}
              >
                <X className="h-4 w-4" />
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="rounded-md border border-dashed p-3 text-xs text-muted-foreground">
          No phone linked yet. Generate a code below and send it to{" "}
          {botName ? `@${botName}` : "the bot"} on Telegram.
        </p>
      )}

      {linkCode ? (
        <div className="rounded-md border border-dashed p-3">
          <div className="text-3xl font-semibold tracking-widest tabular-nums">{linkCode}</div>
          <p className="mt-1.5 text-xs text-muted-foreground">
            Send this to {botName ? `@${botName}` : "the bot"} on Telegram. Valid 10 minutes.
          </p>
        </div>
      ) : null}

      <div className="flex flex-wrap gap-3">
        <Button
          type="button"
          onClick={onGenerate}
          disabled={linking}
          className="bg-green-800 hover:bg-green-900"
        >
          {linking
            ? "Working…"
            : linkCode
              ? "Generate a new code"
              : linked.length > 0
                ? "Link another phone"
                : "Generate link code"}
        </Button>
        {linked.length > 1 ? (
          <Button type="button" variant="outline" onClick={() => setRemovingAll(true)}>
            Disconnect all
          </Button>
        ) : null}
      </div>

      <ConfirmDialog
        open={!!removing}
        onOpenChange={(open) => !open && setRemoving(null)}
        title={`Disconnect ${removing ? describeLink(removing) : "this phone"}?`}
        description="That phone stops receiving notifications and can no longer look candidates up. Your other linked phones are unaffected, and linking it again needs a fresh code."
        actionLabel="Disconnect"
        isPending={busy}
        onConfirm={() => void onRemoveOne()}
      />
      <ConfirmDialog
        open={removingAll}
        onOpenChange={setRemovingAll}
        title="Disconnect every phone?"
        description="All of your linked Telegram accounts are disconnected. Notifications stop entirely until you link one again with a fresh code."
        actionLabel="Disconnect all"
        isPending={busy}
        onConfirm={() => void onRemoveAll()}
      />
    </div>
  );
}
