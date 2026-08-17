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
  SheetFooter,
} from "@/components/ui/sheet";
import { toast } from "sonner";
import type { DepartmentListItem } from "@/lib/schemas/department";

const schema = z.object({
  name: z.string().min(1).max(200),
  code: z
    .string()
    .min(1)
    .max(50)
    .regex(/^[A-Z0-9-]+$/, "Code must be uppercase alphanumeric with hyphens"),
  description: z.string().max(500).optional().nullable(),
});

type FormValues = z.infer<typeof schema>;

type Props = {
  office: DepartmentListItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdated?: () => void;
};

export function EditOfficeSheet({ office, open, onOpenChange, onUpdated }: Props) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  useEffect(() => {
    if (office) {
      reset({
        name: office.name,
        code: office.code,
        description: office.description ?? "",
      });
    }
  }, [office, reset]);

  const onSubmit = async (values: FormValues) => {
    if (!office) return;
    try {
      const res = await fetch(`/api/proxy/departments/${office.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: values.name,
          code: values.code.toUpperCase(),
          description: values.description || null,
          parentDepartmentId: office.parentDepartmentId || null,
          headUserId: null,
          isActive: office.isActive,
        }),
      });
      const result = await res.json().catch(() => ({}));
      if (res.ok && result.isSuccess !== false) {
        toast.success("Office updated successfully");
        onOpenChange(false);
        onUpdated?.();
      } else {
        toast.error(result.error || "Failed to update office");
      }
    } catch {
      toast.error("Failed to update office. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Edit office</SheetTitle>
          <SheetDescription>Update office details.</SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 mt-6 px-1">
          <div className="space-y-1.5">
            <Label htmlFor="edit-name">Name</Label>
            <Input id="edit-name" {...register("name")} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="edit-code">Code</Label>
            <Input id="edit-code" className="uppercase" {...register("code")} />
            {errors.code && (
              <p className="text-xs text-destructive">{errors.code.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="edit-description">Description</Label>
            <Input id="edit-description" {...register("description")} />
          </div>
          <SheetFooter className="pt-4">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="bg-green-800 hover:bg-green-900"
            >
              {isSubmitting ? "Saving…" : "Save"}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
