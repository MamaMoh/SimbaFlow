"use client";

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
import { User, Shield, Key } from "lucide-react";
import { toast } from "sonner";
import { USE_MOCKS } from "@/lib/api/candidates-api";
import { mockApi } from "@/lib/api/mock-api";

const createUserSchema = z.object({
  firstName: z.string().min(2, "First name required"),
  lastName: z.string().min(2, "Last name required"),
  username: z.string().min(3, "Username must be at least 3 characters"),
  email: z.string().email("Valid email required"),
  phoneNumber: z.string().optional(),
  password: z.string().min(8, "Password must be at least 8 characters"),
  role: z.string().min(1, "Role is required"),
  requireMfa: z.boolean().optional(),
});

type CreateUserForm = z.infer<typeof createUserSchema>;

interface CreateUserSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

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

export function CreateUserSheet({ open, onOpenChange, onCreated }: CreateUserSheetProps) {
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

  const onSubmit = async (data: CreateUserForm) => {
    try {
      if (USE_MOCKS) {
        await mockApi.createUser({
          firstName: data.firstName,
          lastName: data.lastName,
          username: data.username,
          email: data.email,
          phoneNumber: data.phoneNumber,
          role: data.role,
          requireMfa: data.requireMfa,
        });
        toast.success("User created");
        reset();
        onOpenChange(false);
        onCreated();
        return;
      }

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
        }),
      });

      const result = await response.json();
      if (result.isSuccess) {
        toast.success("User created");
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
                  {ROLES.map(role => (
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
