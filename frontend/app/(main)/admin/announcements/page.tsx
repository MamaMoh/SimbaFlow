"use client";

import { useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { Megaphone, Pencil, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";

type Level = "Info" | "Warning" | "Critical";

type AnnouncementRow = {
  id: string;
  title: string;
  body: string;
  level: Level;
  tenantId: string | null;
  agency: string | null;
  startsAt: string;
  endsAt: string | null;
  isLive: boolean;
};

type Draft = {
  id?: string;
  title: string;
  body: string;
  level: Level;
  tenantId: string;
  startsAt: string;
  endsAt: string;
};

const ALL = "all";

const fetcher = async (url: string) => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load");
  return body.data;
};

/** A UTC timestamp as the value a datetime-local input expects, in the viewer's own time. */
function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const when = (iso: string) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "short" });

const EMPTY: Draft = { title: "", body: "", level: "Info", tenantId: ALL, startsAt: "", endsAt: "" };

/**
 * Messages from the platform team to agencies. Each shows as a banner above every page for the
 * agencies it is addressed to, between its start and end.
 */
export default function AnnouncementsPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const { data, error, mutate } = useSWR<AnnouncementRow[]>(
    isStaff ? "/api/proxy/platform/announcements" : null,
    fetcher,
  );
  const { data: agencies } = useSWR<{ id: string; name: string }[]>(
    isStaff ? "/api/proxy/tenants" : null,
    fetcher,
    { revalidateOnFocus: false },
  );
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saving, setSaving] = useState(false);

  if (!isLoading && !isStaff) return <AccessDenied resource="announcements" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const rows = data ?? [];

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    try {
      const res = await fetch(
        draft.id ? `/api/proxy/platform/announcements/${draft.id}` : "/api/proxy/platform/announcements",
        {
          method: draft.id ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            title: draft.title,
            body: draft.body,
            level: draft.level,
            tenantId: draft.tenantId === ALL ? null : draft.tenantId,
            startsAt: draft.startsAt ? new Date(draft.startsAt).toISOString() : null,
            endsAt: draft.endsAt ? new Date(draft.endsAt).toISOString() : null,
          }),
        },
      );
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "Could not save the announcement");
        return;
      }
      toast.success(draft.id ? "Announcement updated" : "Announcement posted");
      setDraft(null);
      void mutate();
    } finally {
      setSaving(false);
    }
  };

  const remove = async (row: AnnouncementRow) => {
    const res = await fetch(`/api/proxy/platform/announcements/${row.id}`, { method: "DELETE" });
    const body = await res.json().catch(() => ({}));
    if (res.ok && body?.isSuccess) {
      toast.success("Announcement taken down");
      void mutate();
    } else {
      toast.error(body?.error || "Could not take it down");
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Announcements"
        count={rows.length}
        actions={
          <Button
            size="sm"
            className="h-8 bg-green-800 text-white hover:bg-green-900"
            onClick={() => setDraft({ ...EMPTY })}
          >
            <Megaphone className="mr-1.5 h-3.5 w-3.5" /> New announcement
          </Button>
        }
      />

      {rows.length === 0 ? (
        <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
          Nothing posted. An announcement shows as a banner to the agencies you choose.
        </div>
      ) : (
        <ul className="divide-y rounded-lg border bg-card shadow-sm">
          {rows.map((a) => (
            <li key={a.id} className="flex flex-wrap items-start gap-4 p-4">
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{a.title}</span>
                  <Badge variant={a.level === "Critical" ? "destructive" : "outline"}>{a.level}</Badge>
                  {a.isLive ? (
                    <Badge className="bg-green-100 text-green-800">Showing now</Badge>
                  ) : (
                    <Badge variant="outline" className="text-muted-foreground">Not showing</Badge>
                  )}
                </div>
                <p className="mt-1 line-clamp-2 whitespace-pre-line text-sm text-muted-foreground">{a.body}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {a.agency ?? "All agencies"} · from {when(a.startsAt)}
                  {a.endsAt ? ` to ${when(a.endsAt)}` : ", until taken down"}
                </p>
              </div>
              <div className="flex items-center gap-1.5">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-8"
                  onClick={() =>
                    setDraft({
                      id: a.id,
                      title: a.title,
                      body: a.body,
                      level: a.level,
                      tenantId: a.tenantId ?? ALL,
                      startsAt: toLocalInput(a.startsAt),
                      endsAt: toLocalInput(a.endsAt),
                    })
                  }
                >
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-8 text-muted-foreground hover:text-red-700"
                  onClick={() => remove(a)}
                  aria-label="Take down"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <Dialog open={!!draft} onOpenChange={(open) => { if (!open) setDraft(null); }}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{draft?.id ? "Edit announcement" : "New announcement"}</DialogTitle>
          </DialogHeader>
          {draft && (
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="ann-title">Title</Label>
                <Input
                  id="ann-title"
                  value={draft.title}
                  onChange={(e) => setDraft({ ...draft, title: e.target.value })}
                  placeholder="e.g. Planned maintenance on Saturday night"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="ann-body">Message</Label>
                <Textarea
                  id="ann-body"
                  rows={4}
                  value={draft.body}
                  onChange={(e) => setDraft({ ...draft, body: e.target.value })}
                />
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label>Who sees it</Label>
                  <Select value={draft.tenantId} onValueChange={(v) => setDraft({ ...draft, tenantId: v })}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL}>All agencies</SelectItem>
                      {(agencies ?? []).map((t) => (
                        <SelectItem key={t.id} value={t.id}>
                          {t.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>How loud</Label>
                  <Select value={draft.level} onValueChange={(v) => setDraft({ ...draft, level: v as Level })}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Info">Info</SelectItem>
                      <SelectItem value="Warning">Warning</SelectItem>
                      <SelectItem value="Critical">Critical (cannot be dismissed)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ann-start">Starts</Label>
                  <Input
                    id="ann-start"
                    type="datetime-local"
                    value={draft.startsAt}
                    onChange={(e) => setDraft({ ...draft, startsAt: e.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">Empty means now.</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ann-end">Ends</Label>
                  <Input
                    id="ann-end"
                    type="datetime-local"
                    value={draft.endsAt}
                    onChange={(e) => setDraft({ ...draft, endsAt: e.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">Empty keeps it up until taken down.</p>
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDraft(null)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={saving} className="bg-green-800 hover:bg-green-900">
              {draft?.id ? "Save" : "Post"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
