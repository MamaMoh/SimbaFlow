"use client";

import { useState } from "react";
import useSWR from "swr";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { Shield, FileText } from "lucide-react";
import { toast } from "sonner";
import { USE_MOCKS } from "@/lib/api/candidates-api";
import { mockApi } from "@/lib/api/mock-api";

const createRoleSchema = z.object({
  name: z.string().min(2, "Role name required"),
  code: z.string().min(2, "Code required").regex(/^[a-z][a-z0-9_-]*$/, "Lowercase, alphanumeric, hyphens/underscores"),
  description: z.string().optional(),
  sortOrder: z.number().optional(),
});

type CreateRoleForm = z.infer<typeof createRoleSchema>;

interface CreateRoleSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

const MOCK_PERMISSIONS = [
  { id: "1", code: "candidate.read", name: "Read candidates", module: "Candidates" },
  { id: "2", code: "candidate.write", name: "Write candidates", module: "Candidates" },
  { id: "3", code: "workflow.view", name: "View workflow", module: "Workflow" },
  { id: "4", code: "workflow.configure", name: "Configure workflow", module: "Workflow" },
  { id: "5", code: "embassy.read", name: "Read embassy", module: "Embassy" },
  { id: "6", code: "embassy.write", name: "Write embassy", module: "Embassy" },
  { id: "7", code: "accounting.read", name: "Read accounting", module: "Finance" },
  { id: "8", code: "accounting.write", name: "Write accounting", module: "Finance" },
  { id: "9", code: "staff.read", name: "Read staff", module: "Admin" },
  { id: "10", code: "system.admin", name: "System admin", module: "Admin" },
];

export function CreateRoleSheet({ open, onOpenChange, onCreated }: CreateRoleSheetProps) {
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);

  const { data: permissionsData } = useSWR(
    open && !USE_MOCKS ? "/api/proxy/roles/permissions" : null,
    fetcher
  );
  const permissions: { id: string; code: string; name: string; module: string }[] =
    USE_MOCKS ? MOCK_PERMISSIONS : permissionsData?.data || [];

  // Group permissions by module
  const grouped = permissions.reduce<Record<string, typeof permissions>>((acc, p) => {
    if (!acc[p.module]) acc[p.module] = [];
    acc[p.module].push(p);
    return acc;
  }, {});

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateRoleForm>({
    resolver: zodResolver(createRoleSchema),
    defaultValues: { sortOrder: 0 },
  });

  const togglePermission = (code: string) => {
    setSelectedPermissions(prev =>
      prev.includes(code) ? prev.filter(p => p !== code) : [...prev, code]
    );
  };

  const toggleModule = (module: string) => {
    const moduleCodes = grouped[module].map(p => p.code);
    const allSelected = moduleCodes.every(c => selectedPermissions.includes(c));
    if (allSelected) {
      setSelectedPermissions(prev => prev.filter(c => !moduleCodes.includes(c)));
    } else {
      setSelectedPermissions(prev => [...new Set([...prev, ...moduleCodes])]);
    }
  };

  const onSubmit = async (data: CreateRoleForm) => {
    try {
      if (USE_MOCKS) {
        await mockApi.createRole({
          ...data,
          sortOrder: data.sortOrder || 0,
          permissions: selectedPermissions,
        });
        toast.success("Role created successfully");
        reset();
        setSelectedPermissions([]);
        onOpenChange(false);
        onCreated();
        return;
      }

      const response = await fetch("/api/proxy/roles", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...data,
          sortOrder: data.sortOrder || 0,
          permissions: selectedPermissions,
        }),
      });
      const result = await response.json();
      if (result.isSuccess) {
        toast.success("Role created successfully");
        reset();
        setSelectedPermissions([]);
        onOpenChange(false);
        onCreated();
      } else {
        toast.error(result.error || "Failed to create role");
      }
    } catch {
      toast.error("Failed to create role");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[600px] sm:max-w-[600px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Shield className="h-5 w-5 text-green-700" />
            Create New Role
          </SheetTitle>
          <SheetDescription>
            Define a custom role for your agency and assign permissions.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            {/* Basic Info */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <FileText className="h-4 w-4 text-green-700" />
                Role Details
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Role Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="e.g. Embassy Officer" {...register("name")} />
                  {errors.name && <p className="text-xs text-destructive mt-1">{errors.name.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Code <span className="text-red-500">*</span></Label>
                  <Input placeholder="e.g. embassy-officer" {...register("code")} />
                  {errors.code && <p className="text-xs text-destructive mt-1">{errors.code.message}</p>}
                  <p className="text-xs text-muted-foreground">Lowercase identifier. Cannot be changed later.</p>
                </div>
                <div className="space-y-1.5">
                  <Label>Description</Label>
                  <Textarea placeholder="What can this role do?" {...register("description")} rows={2} />
                </div>
              </div>
            </div>

            <Separator />

            {/* Permissions */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Shield className="h-4 w-4 text-green-700" />
                Permissions ({selectedPermissions.length} selected)
              </h3>

              {Object.entries(grouped).map(([module, perms]) => {
                const allChecked = perms.every(p => selectedPermissions.includes(p.code));
                const someChecked = perms.some(p => selectedPermissions.includes(p.code));

                return (
                  <div key={module} className="mb-4">
                    <div className="flex items-center gap-2 mb-2">
                      <Checkbox
                        checked={allChecked ? true : someChecked ? "indeterminate" : false}
                        onCheckedChange={() => toggleModule(module)}
                      />
                      <span className="text-sm font-medium capitalize">{module}</span>
                      <span className="text-xs text-muted-foreground">({perms.length})</span>
                    </div>
                    <div className="ml-6 grid grid-cols-1 gap-1.5">
                      {perms.map(p => (
                        <label key={p.code} className="flex items-center gap-2 text-sm cursor-pointer hover:bg-muted/50 rounded px-2 py-1">
                          <Checkbox
                            checked={selectedPermissions.includes(p.code)}
                            onCheckedChange={() => togglePermission(p.code)}
                          />
                          <span className="flex-1">{p.name}</span>
                          <code className="text-[10px] text-muted-foreground">{p.code}</code>
                        </label>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Creating..." : "Create Role"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
