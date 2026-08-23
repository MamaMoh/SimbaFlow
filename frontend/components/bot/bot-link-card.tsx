"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { PageAlert } from "@/components/ui/page-alert";
import { botApi, useBotStatus } from "@/lib/api/bot";

/**
 * Personal Telegram linking.
 *
 * Every user links their own account — the code ties one person's Telegram chat to their
 * SimbaFlow user, which is how the bot knows their agency, role and permissions. An owner
 * cannot generate a code for someone else.
 *
 * Rendered on both Settings and Bot & notifications: staff look for it under the bot page,
 * not under settings.
 */
export function BotLinkCard() {
  const [linking, setLinking] = useState(false);
  const [linkCode, setLinkCode] = useState<string | null>(null);
  const { data: botStatus, mutate: mutateBotStatus } = useBotStatus(true);

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

  const onUnlink = async () => {
    setLinking(true);
    try {
      const result = await botApi.unlink();
      if (result?.isSuccess) {
        setLinkCode(null);
        toast.success("Telegram unlinked");
        mutateBotStatus();
      } else {
        toast.error(result?.error || "Could not unlink");
      }
    } catch {
      toast.error("Could not unlink");
    } finally {
      setLinking(false);
    }
  };

  const botName = botStatus?.botUsername;

  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm space-y-4">
      <div>
        <h2 className="text-lg font-semibold">Link my Telegram</h2>
        <p className="text-sm text-muted-foreground">
          Connect your own Telegram so you can look up candidates from your phone.
        </p>
      </div>

      <PageAlert
        variant="info"
        title="Three steps"
        description={
          botName
            ? `Generate a code, open @${botName} on Telegram, and send it as a message.`
            : "Generate a code, open the SimbaFlow bot on Telegram, and send it as a message."
        }
      />

      {linkCode ? (
        <div className="rounded-md border border-dashed p-3">
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            Your code — valid 10 minutes
          </div>
          <div className="mt-1 text-3xl font-semibold tracking-widest">{linkCode}</div>
          <p className="mt-2 text-xs text-muted-foreground">
            Send this to {botName ? `@${botName}` : "the bot"}. Generating a new code cancels this one.
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
          {linking ? "Working…" : linkCode ? "Generate a new code" : "Generate link code"}
        </Button>
        <Button type="button" variant="outline" onClick={onUnlink} disabled={linking}>
          Unlink my Telegram
        </Button>
      </div>
    </div>
  );
}
