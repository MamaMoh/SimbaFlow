"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { FlagBadge } from "@/components/workflow/status-pill";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import {
  deleteDemoOffice,
  getDemoOffices,
  upsertDemoOffice,
} from "@/lib/demo/admin-demo-store";
import type { Office } from "@/types/workflow";

export default function OfficesPage() {
  const { data: offices = [], mutate } = useSWR("demo-offices", () => getDemoOffices(), {
    revalidateOnFocus: false,
  });
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Office | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Office | null>(null);
  const [form, setForm] = useState({ name: "", code: "", city: "", phone: "", email: "", isActive: true });

  const openCreate = () => {
    setEditing(null);
    setForm({ name: "", code: "", city: "", phone: "", email: "", isActive: true });
    setOpen(true);
  };

  const openEdit = (o: Office) => {
    setEditing(o);
    setForm({
      name: o.name,
      code: o.code,
      city: o.city ?? "",
      phone: o.phone ?? "",
      email: o.email ?? "",
      isActive: o.isActive,
    });
    setOpen(true);
  };

  const save = () => {
    if (!form.name.trim() || !form.code.trim()) {
      toast.error("Name and code are required");
      return;
    }
    upsertDemoOffice({ id: editing?.id, ...form });
    toast.success(editing ? "Office updated" : "Office created");
    setOpen(false);
    mutate();
  };

  const activeCount = useMemo(() => offices.filter((o) => o.isActive).length, [offices]);

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Offices</h1>
          <p className="text-sm text-muted-foreground">
            {offices.length} offices · {activeCount} active
          </p>
        </div>
        <Button className="gap-1" onClick={openCreate}>
          <Plus className="h-4 w-4" /> Add office
        </Button>
      </div>

      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        {offices.map((o) => (
          <div key={o.id} className="rounded-xl border bg-card p-4 shadow-sm">
            <div className="flex items-start justify-between gap-2">
              <div>
                <div className="font-semibold">{o.name}</div>
                <div className="mt-1 text-xs font-mono text-muted-foreground">{o.code}</div>
              </div>
              <FlagBadge tone={o.isActive ? "info" : "neutral"}>{o.isActive ? "Active" : "Inactive"}</FlagBadge>
            </div>
            <div className="mt-3 space-y-1 text-sm text-muted-foreground">
              <div>{o.city || "—"}</div>
              <div>{o.phone || "—"}</div>
              <div>{o.email || "—"}</div>
            </div>
            <div className="mt-4 flex gap-1">
              <Button size="sm" variant="outline" className="h-7 gap-1" onClick={() => openEdit(o)}>
                <Pencil className="h-3.5 w-3.5" /> Edit
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="h-7 text-destructive"
                onClick={() => setDeleteTarget(o)}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>
        ))}
      </div>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent className="flex w-full flex-col px-6 sm:max-w-[440px]">
          <SheetHeader>
            <SheetTitle>{editing ? "Edit office" : "Add office"}</SheetTitle>
            <SheetDescription>Branch office used for candidate intake and reporting.</SheetDescription>
          </SheetHeader>
          <div className="mt-4 space-y-3">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Code</Label>
              <Input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>City</Label>
              <Input value={form.city} onChange={(e) => setForm((f) => ({ ...f, city: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Phone</Label>
              <Input value={form.phone} onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
            <div className="flex items-center justify-between rounded-lg border px-3 py-2">
              <span className="text-sm">Active</span>
              <Switch checked={form.isActive} onCheckedChange={(v) => setForm((f) => ({ ...f, isActive: v }))} />
            </div>
            <Button className="w-full" onClick={save}>
              {editing ? "Save changes" : "Create office"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>

      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete office"
        description={`Remove '${deleteTarget?.name}' from the demo agency?`}
        onConfirm={() => {
          if (!deleteTarget) return;
          deleteDemoOffice(deleteTarget.id);
          toast.success("Office deleted");
          setDeleteTarget(null);
          mutate();
        }}
      />
    </div>
  );
}
