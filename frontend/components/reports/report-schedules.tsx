"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Loader2, Mail, Pencil, Plus, Send, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { LoadError } from "@/components/ui/page-alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  reportSchedulesApi,
  useReportSchedules,
  type ReportCatalogItem,
  type ReportSchedule,
  type ReportScheduleInput,
} from "@/lib/api/reports";

const DAYS = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

const HOURS = Array.from({ length: 24 }, (_, h) => h);

function hourLabel(h: number) {
  return `${String(h).padStart(2, "0")}:00`;
}

function formatWhen(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("en-GB", {
    timeZone: "Africa/Addis_Ababa",
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function statusVariant(status: ReportSchedule["lastStatus"]) {
  switch (status) {
    case "Sent":
      return "default" as const;
    case "Partial":
    case "Skipped":
      return "secondary" as const;
    case "Failed":
      return "destructive" as const;
    default:
      return "outline" as const;
  }
}

function emptyInput(reportKey: string): ReportScheduleInput {
  return {
    reportKey,
    format: "excel",
    frequency: "weekly",
    hourLocal: 8,
    dayOfWeek: 1,
    dayOfMonth: 1,
    recipients: "",
    isActive: true,
  };
}

function toInput(s: ReportSchedule): ReportScheduleInput {
  return {
    reportKey: s.reportKey,
    format: s.format,
    frequency: s.frequency,
    hourLocal: s.hourLocal,
    dayOfWeek: s.dayOfWeek ?? 1,
    dayOfMonth: s.dayOfMonth ?? 1,
    recipients: s.recipients.join(", "),
    isActive: s.isActive,
  };
}

/**
 * Reports emailed on a schedule. Each one picks a report from the catalog, a
 * format for the attachment, when it goes out (Addis time) and who gets it.
 */
export function ReportSchedules({ catalog }: { catalog: ReportCatalogItem[] }) {
  const { data, error, isLoading, mutate } = useReportSchedules();
  const [editing, setEditing] = useState<ReportSchedule | "new" | null>(null);
  const [deleting, setDeleting] = useState<ReportSchedule | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const sendNow = async (s: ReportSchedule) => {
    setBusyId(s.id);
    try {
      await reportSchedulesApi.sendNow(s.id);
      toast.success(`${s.reportName} sent to ${s.recipients.length === 1 ? s.recipients[0] : `${s.recipients.length} addresses`}`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not send the report");
    } finally {
      setBusyId(null);
      mutate();
    }
  };

  const toggle = async (s: ReportSchedule, isActive: boolean) => {
    setBusyId(s.id);
    try {
      await reportSchedulesApi.update(s.id, { ...toInput(s), isActive });
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not update the schedule");
    } finally {
      setBusyId(null);
    }
  };

  const remove = async () => {
    if (!deleting) return;
    try {
      await reportSchedulesApi.remove(deleting.id);
      toast.success("Scheduled report removed");
      setDeleting(null);
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not remove the schedule");
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Reports emailed automatically with the full report attached. Times are Addis time.
        </p>
        <Button size="sm" onClick={() => setEditing("new")} disabled={catalog.length === 0}>
          <Plus className="mr-1.5 h-4 w-4" />
          New schedule
        </Button>
      </div>

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      {isLoading && (
        <div className="flex h-32 items-center justify-center text-sm text-muted-foreground">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Loading…
        </div>
      )}

      {data && data.length === 0 && (
        <div className="flex h-40 flex-col items-center justify-center rounded-lg border bg-card text-sm text-muted-foreground">
          <Mail className="mb-2 h-6 w-6 opacity-40" />
          No reports are scheduled yet.
        </div>
      )}

      {data && data.length > 0 && (
        <div className="divide-y rounded-lg border bg-card shadow-sm">
          {data.map((s) => (
            <div key={s.id} className="flex flex-wrap items-start gap-4 p-4">
              <div className="min-w-0 flex-1 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-medium">{s.reportName}</span>
                  <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                    {s.format === "pdf" ? "PDF" : "Excel"}
                  </span>
                  {s.lastStatus && (
                    <Badge variant={statusVariant(s.lastStatus)} title={s.lastError ?? undefined}>
                      {s.lastStatus}
                    </Badge>
                  )}
                </div>
                <p className="text-sm text-muted-foreground">
                  Sent {s.description} to {s.recipients.join(", ")}
                </p>
                <p className="text-xs text-muted-foreground">
                  {s.isActive ? `Next: ${formatWhen(s.nextRunAt)}` : "Paused"}
                  {s.lastRunAt && ` · Last: ${formatWhen(s.lastRunAt)}`}
                  {s.lastError && ` · ${s.lastError}`}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  checked={s.isActive}
                  disabled={busyId === s.id}
                  onCheckedChange={(v) => toggle(s, v)}
                  aria-label={s.isActive ? "Pause schedule" : "Resume schedule"}
                />
                <Button
                  variant="outline"
                  size="sm"
                  disabled={busyId === s.id}
                  onClick={() => sendNow(s)}
                  title="Send it now to check what arrives"
                >
                  {busyId === s.id ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="h-4 w-4" />
                  )}
                  <span className="ml-1.5 hidden sm:inline">Send now</span>
                </Button>
                <Button variant="ghost" size="icon" onClick={() => setEditing(s)} aria-label="Edit schedule">
                  <Pencil className="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="icon" onClick={() => setDeleting(s)} aria-label="Remove schedule">
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {editing && (
        <ScheduleDialog
          catalog={catalog}
          schedule={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            mutate();
          }}
        />
      )}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(o) => !o && setDeleting(null)}
        title="Remove this scheduled report?"
        description={deleting ? `${deleting.reportName} will stop being emailed.` : undefined}
        actionLabel="Remove"
        onConfirm={remove}
      />
    </div>
  );
}

function ScheduleDialog({
  catalog,
  schedule,
  onClose,
  onSaved,
}: {
  catalog: ReportCatalogItem[];
  schedule: ReportSchedule | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<ReportScheduleInput>(() =>
    schedule ? toInput(schedule) : emptyInput(catalog[0]?.key ?? "")
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = <K extends keyof ReportScheduleInput>(key: K, value: ReportScheduleInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const body: ReportScheduleInput = {
        ...form,
        dayOfWeek: form.frequency === "weekly" ? form.dayOfWeek : null,
        dayOfMonth: form.frequency === "monthly" ? form.dayOfMonth : null,
      };
      if (schedule) await reportSchedulesApi.update(schedule.id, body);
      else await reportSchedulesApi.create(body);
      toast.success(schedule ? "Schedule saved" : "Report scheduled");
      onSaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save the schedule");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{schedule ? "Edit scheduled report" : "Schedule a report"}</DialogTitle>
          <DialogDescription>
            The report is built from the latest data each time it goes out.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <div className="grid gap-1.5">
            <Label>Report</Label>
            <Select value={form.reportKey} onValueChange={(v) => set("reportKey", v)}>
              <SelectTrigger>
                <SelectValue placeholder="Pick a report" />
              </SelectTrigger>
              <SelectContent>
                {catalog.map((c) => (
                  <SelectItem key={c.key} value={c.key}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="grid gap-1.5">
              <Label>How often</Label>
              <Select
                value={form.frequency}
                onValueChange={(v) => set("frequency", v as ReportScheduleInput["frequency"])}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="daily">Every day</SelectItem>
                  <SelectItem value="weekly">Every week</SelectItem>
                  <SelectItem value="monthly">Every month</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {form.frequency === "weekly" && (
              <div className="grid gap-1.5">
                <Label>On</Label>
                <Select
                  value={String(form.dayOfWeek ?? 1)}
                  onValueChange={(v) => set("dayOfWeek", Number(v))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {DAYS.map((d, i) => (
                      <SelectItem key={d} value={String(i)}>
                        {d}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            {form.frequency === "monthly" && (
              <div className="grid gap-1.5">
                <Label>On day</Label>
                <Select
                  value={String(form.dayOfMonth ?? 1)}
                  onValueChange={(v) => set("dayOfMonth", Number(v))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Array.from({ length: 28 }, (_, i) => i + 1).map((d) => (
                      <SelectItem key={d} value={String(d)}>
                        {d}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="grid gap-1.5">
              <Label>At (Addis time)</Label>
              <Select
                value={String(form.hourLocal)}
                onValueChange={(v) => set("hourLocal", Number(v))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {HOURS.map((h) => (
                    <SelectItem key={h} value={String(h)}>
                      {hourLabel(h)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label>Attach as</Label>
              <Select
                value={form.format}
                onValueChange={(v) => set("format", v as ReportScheduleInput["format"])}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="excel">Excel</SelectItem>
                  <SelectItem value="pdf">PDF</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="schedule-recipients">Send to</Label>
            <Textarea
              id="schedule-recipients"
              rows={3}
              placeholder="manager@agency.et, finance@agency.et"
              value={form.recipients}
              onChange={(e) => set("recipients", e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Separate addresses with commas or new lines. Up to 20.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Switch
              id="schedule-active"
              checked={form.isActive}
              onCheckedChange={(v) => set("isActive", v)}
            />
            <Label htmlFor="schedule-active">Active</Label>
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving || !form.reportKey}>
            {saving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
            {schedule ? "Save" : "Schedule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
