"use client";

import useSWR from "swr";
import { toast } from "sonner";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";

type AgencyBotSettings = { botDisabled: boolean };

const KEY = "/api/proxy/bot/agency";

const fetcher = async (url: string): Promise<AgencyBotSettings> => {
  const res = await fetch(url, { cache: "no-store" });
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) throw new Error(body?.error || "Could not load");
  return { botDisabled: body?.data?.botDisabled === true };
};

/**
 * The agency's own off switch for the bot.
 *
 * Distinct from a person unlinking their own chat, which is all anybody could do before: this
 * stops the bot answering anyone in the agency and stops every notification, in one place,
 * without asking each member of staff to disconnect.
 *
 * Phrased as "on" because that is how people think about it; what is stored is the opposite, so
 * that an agency which has never seen this setting keeps the bot it already has.
 */
export function AgencyBotSwitch() {
  const { data, mutate, isLoading } = useSWR(KEY, fetcher, { revalidateOnFocus: false });
  const on = data ? !data.botDisabled : true;

  const set = async (next: boolean) => {
    // Shown immediately, rolled back by the revalidate if the save fails.
    void mutate({ botDisabled: !next }, { revalidate: false });
    try {
      const res = await fetch(KEY, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ botDisabled: !next }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) throw new Error(body?.error || "Could not save");
      toast.success(next ? "The bot is on for this agency" : "The bot is off for this agency");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save");
    } finally {
      void mutate();
    }
  };

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border bg-card p-5 shadow-sm">
      <div>
        <Label htmlFor="agency-bot" className="text-sm font-semibold">
          Telegram bot for this agency
        </Label>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Off stops it answering anyone here and stops every notification. Nobody has to unlink.
        </p>
      </div>
      <Switch
        id="agency-bot"
        checked={on}
        disabled={isLoading}
        onCheckedChange={(v) => void set(v === true)}
      />
    </div>
  );
}
