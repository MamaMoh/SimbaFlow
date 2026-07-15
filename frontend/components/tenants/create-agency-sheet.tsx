"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
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
import { Building2, User, Key } from "lucide-react";
import { toast } from "sonner";
import { PhoneInputField } from "@/components/ui/phone-input";

const createAgencySchema = z.object({
  agencyName: z.string().min(3, "Agency name must be at least 3 characters"),
  slug: z.string().min(3).max(50).regex(/^[a-z][a-z0-9-]*[a-z0-9]$/, "Lowercase, alphanumeric with hyphens"),
  contactEmail: z.string().email("Valid email required"),
  contactPhone: z.string().optional(),
  adminFirstName: z.string().min(2, "First name required"),
  adminLastName: z.string().min(2, "Last name required"),
  adminEmail: z.string().email("Valid admin email required"),
  temporaryPassword: z.string().min(8, "Password must be at least 8 characters"),
});

type CreateAgencyForm = z.infer<typeof createAgencySchema>;

interface CreateAgencySheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

export function CreateAgencySheet({ open, onOpenChange, onCreated }: CreateAgencySheetProps) {
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateAgencyForm>({
    resolver: zodResolver(createAgencySchema),
    defaultValues: { temporaryPassword: "Welcome@123!" },
  });

  // Auto-generate slug from agency name
  const agencyName = watch("agencyName");
  const generateSlug = () => {
    if (agencyName) {
      const slug = agencyName
        .toLowerCase()
        .replace(/[^a-z0-9\s-]/g, "")
        .replace(/\s+/g, "-")
        .replace(/-+/g, "-")
        .replace(/^-|-$/g, "");
      setValue("slug", slug);
    }
  };

  const onSubmit = async (data: CreateAgencyForm) => {
    try {
      const response = await fetch("/api/proxy/tenants", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      const result = await response.json();
      if (result.isSuccess) {
        toast.success(`Agency "${data.agencyName}" created successfully`);
        reset();
        onOpenChange(false);
        onCreated();
      } else {
        toast.error(result.error || "Failed to create agency");
      }
    } catch {
      toast.error("Failed to create agency");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[540px] sm:max-w-[540px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Building2 className="h-5 w-5 text-green-700" />
            Create New Agency
          </SheetTitle>
          <SheetDescription>
            Set up a new labour export agency with its own isolated data and admin account.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            {/* Agency Details */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Building2 className="h-4 w-4 text-green-700" />
                Agency Details
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Agency Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="e.g. Ethio Star Labour Export" {...register("agencyName")} onBlur={generateSlug} />
                  {errors.agencyName && <p className="text-xs text-destructive mt-1">{errors.agencyName.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Slug (URL identifier) <span className="text-red-500">*</span></Label>
                  <Input placeholder="e.g. ethio-star" {...register("slug")} />
                  {errors.slug && <p className="text-xs text-destructive mt-1">{errors.slug.message}</p>}
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

            {/* Agency Owner Account */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <User className="h-4 w-4 text-green-700" />
                Agency Owner Account
              </h3>
              <p className="text-xs text-muted-foreground mb-4">
                This user will be the agency administrator with full access within their agency.
              </p>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>First Name <span className="text-red-500">*</span></Label>
                    <Input placeholder="First name" {...register("adminFirstName")} />
                    {errors.adminFirstName && <p className="text-xs text-destructive mt-1">{errors.adminFirstName.message}</p>}
                  </div>
                  <div className="space-y-1.5">
                    <Label>Last Name <span className="text-red-500">*</span></Label>
                    <Input placeholder="Last name" {...register("adminLastName")} />
                    {errors.adminLastName && <p className="text-xs text-destructive mt-1">{errors.adminLastName.message}</p>}
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Admin Email <span className="text-red-500">*</span></Label>
                  <Input type="email" placeholder="owner@agency.com" {...register("adminEmail")} />
                  {errors.adminEmail && <p className="text-xs text-destructive mt-1">{errors.adminEmail.message}</p>}
                  <p className="text-xs text-muted-foreground">This will be their login username.</p>
                </div>
              </div>
            </div>

            <Separator />

            {/* Initial Password */}
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Key className="h-4 w-4 text-green-700" />
                Initial Password
              </h3>
              <div className="space-y-1.5">
                <Label>Temporary Password <span className="text-red-500">*</span></Label>
                <Input type="password" {...register("temporaryPassword")} />
                {errors.temporaryPassword && <p className="text-xs text-destructive mt-1">{errors.temporaryPassword.message}</p>}
                <p className="text-xs text-muted-foreground">The agency owner will be forced to change this on first login.</p>
              </div>
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Creating..." : "Create Agency"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
