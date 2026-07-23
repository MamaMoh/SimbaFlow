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
  deleteDemoDepartment,
  getDemoDepartments,
  upsertDemoDepartment,
  type DemoDepartment,
} from "@/lib/demo/admin-demo-store";

export default function DepartmentsPage() {
  const { data: departments = [], mutate } = useSWR("demo-departments", () => getDemoDepartments(), {
    revalidateOnFocus: false,
  });
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<DemoDepartment | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DemoDepartment | null>(null);
  const [form, setForm] = useState({ name: "", code: "", description: "", isActive: true });

  const openCreate = () => {
    setEditing(null);
    setForm({ name: "", code: "", description: "", isActive: true });
    setOpen(true);
  };

  const openEdit = (d: DemoDepartment) => {
    setEditing(d);
    setForm({
      name: d.name,
      code: d.code,
      description: d.description ?? "",
      isActive: d.isActive,
    });
    setOpen(true);
  };

  const save = () => {
    if (!form.name.trim() || !form.code.trim()) {
      toast.error("Name and code are required");
      return;
    }
    upsertDemoDepartment({ id: editing?.id, ...form });
    toast.success(editing ? "Department updated" : "Department created");
    setOpen(false);
    mutate();
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Departments</h1>
          <p className="text-sm text-muted-foreground">Organize staff into departments</p>
        </div>
        <Button className="gap-1" onClick={openCreate}>
          <Plus className="h-4 w-4" /> Add department
        </Button>
      </div>

      <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-3">Name</th>
              <th className="p-3">Code</th>
              <th className="p-3">Description</th>
              <th className="p-3">Status</th>
              <th className="p-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {departments.map((d) => (
              <tr key={d.id} className="border-t">
                <td className="p-3 font-medium">{d.name}</td>
                <td className="p-3 font-mono text-xs">{d.code}</td>
                <td className="p-3 text-muted-foreground">{d.description || "—"}</td>
                <td className="p-3">
                  <FlagBadge tone={d.isActive ? "info" : "neutral"}>
                    {d.isActive ? "Active" : "Inactive"}
                  </FlagBadge>
                </td>
                <td className="p-3">
                  <div className="flex justify-end gap-1">
                    <Button size="sm" variant="ghost" className="h-7" onClick={() => openEdit(d)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 text-destructive"
                      onClick={() => setDeleteTarget(d)}
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
            <SheetTitle>{editing ? "Edit department" : "Add department"}</SheetTitle>
            <SheetDescription>Used for staff assignment and reporting.</SheetDescription>
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
              <Label>Description</Label>
              <Input
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div className="flex items-center justify-between rounded-lg border px-3 py-2">
              <span className="text-sm">Active</span>
              <Switch checked={form.isActive} onCheckedChange={(v) => setForm((f) => ({ ...f, isActive: v }))} />
            </div>
            <Button className="w-full" onClick={save}>
              {editing ? "Save changes" : "Create department"}
            </Button>
          </div>
        </SheetContent>
      </Sheet>

      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete department"
        description={`Remove '${deleteTarget?.name}'?`}
        onConfirm={() => {
          if (!deleteTarget) return;
          deleteDemoDepartment(deleteTarget.id);
          toast.success("Department deleted");
          setDeleteTarget(null);
          mutate();
        }}
      />
    </div>
  );
}
