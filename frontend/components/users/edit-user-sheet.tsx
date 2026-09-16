"use client";

import { useEffect } from "react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { User, Shield, Building2 } from "lucide-react";
import { toast } from "sonner";

const editUserSchema = z.object({
  firstName: z.string().min(2, "First name required"),
  lastName: z.string().min(2, "Last name required"),
  middleName: z.string().optional(),
  email: z.string().email("Valid email required"),
  phoneNumber: z.string().optional(),
  role: z.string().optional(),
});

type EditUserForm = z.infer<typeof editUserSchema>;

export interface EditableUser {
  id: string;
  username: string;
  firstName: string;
  lastName: string;
  middleName?: string | null;
  email: string;
  phoneNumber: string | null;
  isSuperAdmin: boolean;
  tenantName: string | null;
  roles: string[];
}

/**
 * Roles an agency's own administrator may assign.
 *
 * SuperAdmin and PlatformAdmin are deliberately absent: they reach across every agency on the
 * platform, and the API refuses to grant them to anyone who does not already hold the platform.
 * Offering them here would only produce a request that comes back 403.
 */
const ASSIGNABLE_ROLES = [
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

interface EditUserSheetProps {
  user: EditableUser | null;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}

export function EditUserSheet({ user, onOpenChange, onSaved }: EditUserSheetProps) {
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<EditUserForm>({ resolver: zodResolver(editUserSchema) });

  // Reload the form whenever a different person is opened, so the sheet never shows the last one.
  useEffect(() => {
    if (!user) return;
    reset({
      firstName: user.firstName ?? "",
      lastName: user.lastName ?? "",
      middleName: user.middleName ?? "",
      email: user.email ?? "",
      phoneNumber: user.phoneNumber ?? "",
      role: user.roles?.[0] ?? "",
    });
  }, [user, reset]);

  const selectedRole = watch("role");

  const onSubmit = async (data: EditUserForm) => {
    if (!user) return;

    try {
      const response = await fetch(`/api/proxy/users/${user.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: user.id,
          firstName: data.firstName,
          lastName: data.lastName,
          middleName: data.middleName || null,
          email: data.email,
          phoneNumber: data.phoneNumber || null,
          departmentId: null,
          // Never sent from here. Whether someone administers the whole platform is not an edit
          // made on an agency's staff screen, and the API would refuse it anyway.
          isSuperAdmin: user.isSuperAdmin,
        }),
      });

      const result = await response.json();
      if (!result.isSuccess) {
        toast.error(result.error || "Could not save this user.");
        return;
      }

      // The role lives on its own endpoint, and only goes if it actually changed — so saving a
      // phone number does not rewrite someone's access as a side effect.
      const currentRole = user.roles?.[0] ?? "";
      if (data.role && data.role !== currentRole) {
        const roleResponse = await fetch(`/api/proxy/users/${user.id}/roles`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ roleNames: [data.role] }),
        });
        const roleResult = await roleResponse.json();
        if (!roleResult.isSuccess) {
          toast.error(roleResult.error || "Saved their details, but the role could not be changed.");
          onSaved();
          return;
        }
      }

      toast.success(`Saved ${data.firstName} ${data.lastName}.`);
      onOpenChange(false);
      onSaved();
    } catch {
      toast.error("Could not save this user. Please try again.");
    }
  };

  return (
    <Sheet open={!!user} onOpenChange={onOpenChange}>
      <SheetContent className="w-[480px] sm:max-w-[480px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <User className="h-5 w-5 text-green-700" />
            Edit user
          </SheetTitle>
          <SheetDescription>
            {user ? (
              <>
                <span className="font-medium text-foreground">@{user.username}</span>
                {user.tenantName ? ` · ${user.tenantName}` : " · Platform"}
              </>
            ) : null}
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <User className="h-4 w-4 text-green-700" />
                Details
              </h3>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>First name <span className="text-red-500">*</span></Label>
                    <Input {...register("firstName")} />
                    {errors.firstName && <p className="text-xs text-destructive mt-1">{errors.firstName.message}</p>}
                  </div>
                  <div className="space-y-1.5">
                    <Label>Last name <span className="text-red-500">*</span></Label>
                    <Input {...register("lastName")} />
                    {errors.lastName && <p className="text-xs text-destructive mt-1">{errors.lastName.message}</p>}
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Middle name</Label>
                  <Input {...register("middleName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Email <span className="text-red-500">*</span></Label>
                  <Input type="email" {...register("email")} />
                  {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Phone number</Label>
                  <Input {...register("phoneNumber")} />
                </div>
                <div className="space-y-1.5">
                  <Label className="flex items-center gap-1.5 text-muted-foreground">
                    <Building2 className="h-3.5 w-3.5" /> Username and agency
                  </Label>
                  <p className="text-sm text-muted-foreground">
                    A username is how someone signs in and cannot be changed here. Moving a person
                    between agencies is done from Agencies.
                  </p>
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Shield className="h-4 w-4 text-green-700" />
                Role
              </h3>
              {user?.isSuperAdmin ? (
                <p className="text-sm text-muted-foreground">
                  This is a platform administrator. Their role is managed at platform level and is
                  not editable from here.
                </p>
              ) : (
                <div className="space-y-1.5">
                  <Label>Role</Label>
                  <Select value={selectedRole || ""} onValueChange={(val) => setValue("role", val)}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select a role" />
                    </SelectTrigger>
                    <SelectContent>
                      {ASSIGNABLE_ROLES.map((role) => (
                        <SelectItem key={role} value={role}>{role}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    Changing this changes what they can see and do. It takes effect the next time
                    they sign in.
                  </p>
                </div>
              )}
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Saving…" : "Save changes"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
