"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import useSWR from "swr";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { Building2, Pencil, User, Key } from "lucide-react";
import { toast } from "sonner";
import { PhoneInputField } from "@/components/ui/phone-input";

const editAgencySchema = z.object({
  name: z.string().min(3, "Agency name must be at least 3 characters"),
  contactEmail: z.string().email("Valid email required"),
  contactPhone: z.string().optional(),
  maxUsers: z.number().min(1).optional(),
});

type EditAgencyForm = z.infer<typeof editAgencySchema>;

interface EditAgencySheetProps {
  agencyId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdated: () => void;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

export function EditAgencySheet({ agencyId, open, onOpenChange, onUpdated }: EditAgencySheetProps) {
  const { data } = useSWR(
    agencyId && open ? `/api/proxy/tenants/${agencyId}` : null,
    fetcher
  );

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<EditAgencyForm>({
    resolver: zodResolver(editAgencySchema),
  });

  useEffect(() => {
    if (data?.data) {
      reset({
        name: data.data.name || "",
        contactEmail: data.data.contactEmail || "",
        contactPhone: data.data.contactPhone || "",
        maxUsers: data.data.maxUsers || 50,
      });
    }
  }, [data, reset]);

  const onSubmit = async (formData: EditAgencyForm) => {
    if (!agencyId) return;
    try {
      const res = await fetch(`/api/proxy/tenants/${agencyId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formData),
      });
      const result = await res.json();
      if (result.isSuccess) {
        toast.success("Agency updated successfully");
        onOpenChange(false);
        onUpdated();
      } else {
        toast.error(result.error || "Failed to update agency");
      }
    } catch {
      toast.error("Failed to update agency");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[540px] sm:max-w-[540px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Pencil className="h-5 w-5 text-green-700" />
            Edit Agency
          </SheetTitle>
          <SheetDescription>
            Update agency details. Slug and schema cannot be changed after creation.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            {/* Agency Details — same as Create */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Building2 className="h-4 w-4 text-green-700" />
                Agency Details
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Agency Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="e.g. Ethio Star Labour Export" {...register("name")} />
                  {errors.name && <p className="text-xs text-destructive mt-1">{errors.name.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Slug (URL identifier)</Label>
                  <Input value={data?.data?.slug || ""} disabled className="bg-muted" />
                  <p className="text-xs text-muted-foreground">Used for database schema. Cannot be changed later.</p>
                </div>
                <div className="space-y-1.5">
                  <Label>Contact Email <span className="text-red-500">*</span></Label>
                  <Input type="email" placeholder="agency@example.com" {...register("contactEmail")} />
                  {errors.contactEmail && <p className="text-xs text-destructive mt-1">{errors.contactEmail.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Contact Phone</Label>
                  <PhoneInputField value={watch("contactPhone") || ""} onChange={(val) => setValue("contactPhone", val)} />
                </div>
              </div>
            </div>

            <Separator />

            {/* Agency Owner Account — show actual owner data */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <User className="h-4 w-4 text-green-700" />
                Agency Owner Account
              </h3>
              <p className="text-xs text-muted-foreground mb-4">
                The agency administrator. To change, use the Staff management page.
              </p>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>First Name</Label>
                    <Input value={data?.data?.ownerFirstName || "—"} disabled className="bg-muted" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Last Name</Label>
                    <Input value={data?.data?.ownerLastName || "—"} disabled className="bg-muted" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Admin Email</Label>
                  <Input value={data?.data?.ownerEmail || "—"} disabled className="bg-muted" />
                  <p className="text-xs text-muted-foreground">This is their login username.</p>
                </div>
              </div>
            </div>

            <Separator />

            {/* Initial Password — same section as Create but informational */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Key className="h-4 w-4 text-green-700" />
                Settings
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Max Users</Label>
                  <Input type="number" placeholder="50" {...register("maxUsers", { valueAsNumber: true })} />
                  <p className="text-xs text-muted-foreground">Maximum number of user accounts for this agency.</p>
                </div>
                <div className="space-y-1.5">
                  <Label>Schema Name</Label>
                  <Input value={data?.data?.schemaName || ""} disabled className="bg-muted font-mono text-sm" />
                  <p className="text-xs text-muted-foreground">Database schema. Cannot be changed.</p>
                </div>
              </div>
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Saving..." : "Save Changes"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
