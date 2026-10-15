"use client";

import { useEffect, useState } from "react";
import useSWR from "swr";
import { Info, AlertTriangle, OctagonAlert, X } from "lucide-react";

type Announcement = {
  id: string;
  title: string;
  body: string;
  level: "Info" | "Warning" | "Critical";
};

const KEY = "simba_dismissed_announcements";

const fetcher = async (url: string): Promise<Announcement[]> => {
  const res = await fetch(url);
  if (!res.ok) return [];
  const body = await res.json().catch(() => null);
  return Array.isArray(body?.data) ? body.data : [];
};

function readDismissed(): string[] {
  try {
    const raw = window.localStorage.getItem(KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function writeDismissed(ids: string[]) {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(ids.slice(-50)));
  } catch {
    // Not remembered; it shows again next time.
  }
}

const STYLE = {
  Info: { box: "border-blue-200 bg-blue-50 text-blue-900", Icon: Info },
  Warning: { box: "border-amber-200 bg-amber-50 text-amber-900", Icon: AlertTriangle },
  Critical: { box: "border-red-200 bg-red-50 text-red-900", Icon: OctagonAlert },
} as const;

/**
 * Messages from the platform team, shown above every page while they are current. Each person can
 * dismiss one for themselves, which this browser remembers. Critical ones cannot be dismissed.
 */
export function AnnouncementBanner() {
  const { data } = useSWR("/api/proxy/announcements/active", fetcher, {
    refreshInterval: 5 * 60_000,
    revalidateOnFocus: false,
  });
  const [dismissed, setDismissed] = useState<string[]>([]);

  useEffect(() => setDismissed(readDismissed()), []);

  const visible = (data ?? []).filter((a) => a.level === "Critical" || !dismissed.includes(a.id));
  if (visible.length === 0) return null;

  const dismiss = (id: string) => {
    const next = [...dismissed, id];
    setDismissed(next);
    writeDismissed(next);
  };

  return (
    <div className="flex flex-col gap-2">
      {visible.map((a) => {
        const { box, Icon } = STYLE[a.level] ?? STYLE.Info;
        return (
          <div key={a.id} className={`flex items-start gap-3 rounded-lg border px-4 py-3 text-sm ${box}`} role="status">
            <Icon className="mt-0.5 h-4 w-4 shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="font-semibold">{a.title}</p>
              <p className="whitespace-pre-line">{a.body}</p>
            </div>
            {a.level !== "Critical" && (
              <button
                type="button"
                onClick={() => dismiss(a.id)}
                className="rounded p-1 opacity-70 hover:opacity-100"
                aria-label="Dismiss"
              >
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
        );
      })}
    </div>
  );
}
