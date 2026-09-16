"use client";

import { useEffect, useState } from "react";
import useSWR from "swr";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { User, Shield, Key, Building2 } from "lucide-react";
import { toast } from "sonner";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { getActingTenantId } from "@/lib/tenant/acting-tenant";

const PLATFORM_ONLY = "__platform__";

const createUserSchema = z.object({
  firstName: z.string().min(2, "First name required"),
  lastName: z.string().min(2, "Last name required"),
  username: z.string().min(3, "Username must be at least 3 characters"),
  email: z.string().email("Valid email required"),
  phoneNumber: z.string().optional(),
  password: z.string().min(8, "Password must be at least 8 characters"),
  role: z.string().min(1, "Role is required"),
  requireMfa: z.boolean().optional(),
  tenantId: z.string().optional(),
});

type CreateUserForm = z.infer<typeof createUserSchema>;

interface CreateUserSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

/** Roles that run the platform and so have no agency of their own. */
const PLATFORM_ROLES = ["PlatformAdmin"];

const ROLES = [
  "AgencyOwner",
  "OfficeManager",
  "EmbassyOfficer",
  "CaseExecutive",
  "FinanceOfficer",
  "FieldAgent",
  "DataEntryClerk",
  "Auditor",
  "NotificationManager",
];

type Agency = { id: string; name: string };

const agencyFetcher = (url: string) => fetch(url).then((r) => r.json());

export function CreateUserSheet({ open, onOpenChange, onCreated }: CreateUserSheetProps) {
  // Only a platform administrator chooses the agency — everyone else creates people in their own,
  // which the API enforces regardless of what is sent.
  const { isSuperAdmin } = usePermissions();
  const { data: agencyData } = useSWR(
    open && isSuperAdmin ? "/api/proxy/tenants" : null,
    agencyFetcher,
    { revalidateOnFocus: false },
  );
  const agencies: Agency[] = Array.isArray(agencyData)
    ? agencyData
    : Array.isArray(agencyData?.data)
      ? agencyData.data
      : Array.isArray(agencyData?.items)
        ? agencyData.items
        : [];

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateUserForm>({
    resolver: zodResolver(createUserSchema),
    defaultValues: { requireMfa: false },
  });

  const selectedTenant = watch("tenantId");
  const selectedRole = watch("role");

  // Default to whichever agency they are already working inside, so the common case is one click.
  useEffect(() => {
    if (!open || !isSuperAdmin) return;
    const acting = getActingTenantId();
    if (acting) setValue("tenantId", acting);
  }, [open, isSuperAdmin, setValue]);

  const onSubmit = async (data: CreateUserForm) => {
    try {
      const response = await fetch("/api/proxy/users", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName: data.firstName,
          lastName: data.lastName,
          username: data.username,
          email: data.email,
          phoneNumber: data.phoneNumber || null,
          password: data.password,
          roleName: data.role,
          requireMfa: data.requireMfa || false,
          // Null means a platform account with no agency, which the API accepts only for a
          // platform role. A tenant admin's value is ignored — the API uses their own agency.
          tenantId:
            data.tenantId && data.tenantId !== PLATFORM_ONLY ? data.tenantId : null,
        }),
      });

      const result = await response.json();
      if (result.isSuccess) {
        reset();
        onOpenChange(false);
        onCreated();
      } else {
        toast.error(result.error || "Failed to create user");
      }
    } catch {
      toast.error("Failed to create user. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[480px] sm:max-w-[480px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <User className="h-5 w-5 text-green-700" />
            Add New User
          </SheetTitle>
          <SheetDescription>
            Create a new user account with role assignment.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
          {/* Agency — platform administrators only; everyone else creates inside their own. */}
          {isSuperAdmin && (
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Building2 className="h-4 w-4 text-green-700" />
                Agency
              </h3>
              <div className="space-y-1.5">
                <Label>Belongs to <span className="text-red-500">*</span></Label>
                <Select
                  value={selectedTenant || ""}
                  onValueChange={(val) => setValue("tenantId", val)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Choose an agency" />
                  </SelectTrigger>
                  <SelectContent>
                    {agencies.map((a) => (
                      <SelectItem key={a.id} value={a.id}>{a.name}</SelectItem>
                    ))}
                    <SelectItem value={PLATFORM_ONLY}>
                      Platform administrator — no agency
                    </SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {selectedTenant === PLATFORM_ONLY
                    ? "Choose a platform role below. An agency role without an agency cannot reach any data."
                    : "Everyone works inside an agency, apart from the people who run the platform."}
                </p>
              </div>
            </div>
          )}

          {isSuperAdmin && <Separator />}

          {/* Basic Information */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <User className="h-4 w-4 text-green-700" />
              Basic Information
            </h3>
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>First Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="First name" {...register("firstName")} />
                  {errors.firstName && <p className="text-xs text-destructive mt-1">{errors.firstName.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Last Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="Last name" {...register("lastName")} />
                  {errors.lastName && <p className="text-xs text-destructive mt-1">{errors.lastName.message}</p>}
                </div>
              </div>
              <div className="space-y-1.5">
                <Label>Username <span className="text-red-500">*</span></Label>
                <Input placeholder="Enter username" {...register("username")} />
                {errors.username && <p className="text-xs text-destructive mt-1">{errors.username.message}</p>}
              </div>
              <div className="space-y-1.5">
                <Label>Email <span className="text-red-500">*</span></Label>
                <Input type="email" placeholder="Enter email address" {...register("email")} />
                {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
              </div>
              <div className="space-y-1.5">
                <Label>Phone Number</Label>
                <Input placeholder="Enter phone number" {...register("phoneNumber")} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Security */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <Key className="h-4 w-4 text-green-700" />
              Security
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label>Password <span className="text-red-500">*</span></Label>
                <Input type="password" placeholder="Min 8 chars, uppercase, lowercase, digit, special" {...register("password")} />
                {errors.password && <p className="text-xs text-destructive mt-1">{errors.password.message}</p>}
              </div>
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="requireMfa"
                  onCheckedChange={(checked) => setValue("requireMfa", !!checked)}
                />
                <Label htmlFor="requireMfa" className="text-sm font-normal">Require MFA (Multi-Factor Authentication)</Label>
              </div>
            </div>
          </div>

          <Separator />

          {/* Role Assignment */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <Shield className="h-4 w-4 text-green-700" />
              Role Assignment
            </h3>
            <div className="space-y-1.5">
              <Label>Role <span className="text-red-500">*</span></Label>
              <Select onValueChange={(val) => setValue("role", val)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a role" />
                </SelectTrigger>
                <SelectContent>
                  {(selectedTenant === PLATFORM_ONLY ? PLATFORM_ROLES : ROLES).map(role => (
                    <SelectItem key={role} value={role}>{role}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {errors.role && <p className="text-xs text-destructive mt-1">{errors.role.message}</p>}
            </div>
          </div>

          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Creating..." : "Create User"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
