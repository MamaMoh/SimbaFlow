"use client";

import { useState } from "react";
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
  deleteDemoPartner,
  getDemoPartners,
  upsertDemoPartner,
  type DemoPartner,
} from "@/lib/demo/admin-demo-store";

export default function PartnersPage() {
  const { data: partners = [], mutate } = useSWR("demo-partners", () => getDemoPartners(), {
    revalidateOnFocus: false,
  });
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<DemoPartner | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DemoPartner | null>(null);
  const [form, setForm] = useState({ name: "", country: "", phone: "", email: "", isActive: true });

  const openCreate = () => {
    setEditing(null);
    setForm({ name: "", country: "", phone: "", email: "", isActive: true });
    setOpen(true);
  };

  const openEdit = (p: DemoPartner) => {
    setEditing(p);
    setForm({
      name: p.name,
      country: p.country,
      phone: p.phone,
      email: p.email ?? "",
      isActive: p.isActive,
    });
    setOpen(true);
  };

  const save = () => {
    if (!form.name.trim() || !form.country.trim()) {
      toast.error("Name and country are required");
      return;
    }
    upsertDemoPartner({ id: editing?.id, ...form });
    toast.success(editing ? "Partner updated" : "Partner created");
    setOpen(false);
    mutate();
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Partners</h1>
          <p className="text-sm text-muted-foreground">Overseas sponsors / partner agencies</p>
        </div>
        <Button className="gap-1" onClick={openCreate}>
          <Plus className="h-4 w-4" /> Add partner
        </Button>
      </div>

      <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-3">Name</th>
              <th className="p-3">Country</th>
              <th className="p-3">Phone</th>
              <th className="p-3">Status</th>
              <th className="p-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {partners.map((p) => (
              <tr key={p.id} className="border-t">
                <td className="p-3 font-medium">{p.name}</td>
                <td className="p-3">{p.country}</td>
                <td className="p-3 font-mono text-xs">{p.phone || "—"}</td>
                <td className="p-3">
                  <FlagBadge tone={p.isActive ? "info" : "neutral"}>
                    {p.isActive ? "Active" : "Inactive"}
                  </FlagBadge>
                </td>
                <td className="p-3">
                  <div className="flex justify-end gap-1">
                    <Button size="sm" variant="ghost" className="h-7" onClick={() => openEdit(p)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 text-destructive"
                      onClick={() => setDeleteTarget(p)}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent className="flex w-full flex-col px-6 sm:max-w-[440px]">
          <SheetHeader>
            <SheetTitle>{editing ? "Edit partner" : "Add partner"}</SheetTitle>
            <SheetDescription>Sponsor or overseas recruitment partner.</SheetDescription>
          </SheetHeader>
          <div className="mt-4 space-y-3">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Country</Label>
              <Input value={form.country} onChange={(e) => setForm((f) => ({ ...f, country: e.target.value }))} />
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
              {editing ? "Save changes" : "Create partner"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>

      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete partner"
        description={`Remove '${deleteTarget?.name}'?`}
        onConfirm={() => {
          if (!deleteTarget) return;
          deleteDemoPartner(deleteTarget.id);
          toast.success("Partner deleted");
          setDeleteTarget(null);
          mutate();
        }}
      />
    </div>
  );
}
