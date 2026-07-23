"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { getDemoSettings, saveDemoSettings, type DemoSettings } from "@/lib/demo/admin-demo-store";

export default function SettingsPage() {
  const [form, setForm] = useState<DemoSettings>(getDemoSettings());

  useEffect(() => {
    setForm(getDemoSettings());
  }, []);

  const save = () => {
    saveDemoSettings(form);
    toast.success("Settings saved");
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Settings</h1>
        <p className="text-sm text-muted-foreground">Agency preferences (demo, in-session)</p>
      </div>

      <div className="max-w-xl space-y-4 rounded-xl border bg-card p-4 shadow-sm">
        <div className="space-y-2">
          <Label>Agency display name</Label>
          <Input
            value={form.agencyName}
            onChange={(e) => setForm((f) => ({ ...f, agencyName: e.target.value }))}
          />
        </div>
        <div className="space-y-2">
          <Label>Timezone</Label>
          <Select value={form.timezone} onValueChange={(v) => setForm((f) => ({ ...f, timezone: v }))}>
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Africa/Addis_Ababa">Africa/Addis_Ababa</SelectItem>
              <SelectItem value="Asia/Riyadh">Asia/Riyadh</SelectItem>
              <SelectItem value="Asia/Dubai">Asia/Dubai</SelectItem>
              <SelectItem value="UTC">UTC</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-2">
            <Label>Default currency</Label>
            <Select
              value={form.defaultCurrency}
              onValueChange={(v) => setForm((f) => ({ ...f, defaultCurrency: v }))}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ETB">ETB</SelectItem>
                <SelectItem value="USD">USD</SelectItem>
                <SelectItem value="SAR">SAR</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Language</Label>
            <Select value={form.language} onValueChange={(v) => setForm((f) => ({ ...f, language: v }))}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="en">English</SelectItem>
                <SelectItem value="am">Amharic</SelectItem>
                <SelectItem value="ar">Arabic</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Email notifications</div>
              <div className="text-xs text-muted-foreground">Stage SLA and overdue alerts</div>
            </div>
            <Switch
              checked={form.notifyEmail}
              onCheckedChange={(v) => setForm((f) => ({ ...f, notifyEmail: v }))}
            />
          </div>
          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">SMS notifications</div>
              <div className="text-xs text-muted-foreground">Candidate flight reminders</div>
            </div>
            <Switch checked={form.notifySms} onCheckedChange={(v) => setForm((f) => ({ ...f, notifySms: v }))} />
          </div>
          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Auto-assign cases</div>
              <div className="text-xs text-muted-foreground">Round-robin to case executives</div>
            </div>
            <Switch
              checked={form.autoAssignCase}
              onCheckedChange={(v) => setForm((f) => ({ ...f, autoAssignCase: v }))}
            />
          </div>
        </div>

        <Button onClick={save}>Save settings</Button>
      </div>
    </div>
  );
}
