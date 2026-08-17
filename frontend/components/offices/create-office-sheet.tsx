"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
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
import { CreateDepartmentSchema, type CreateDepartment } from "@/lib/schemas/department";
import { toast } from "sonner";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated?: () => void;
};

export function CreateOfficeSheet({ open, onOpenChange, onCreated }: Props) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateDepartment>({
    resolver: zodResolver(CreateDepartmentSchema),
    defaultValues: { name: "", code: "", description: "", isActive: true },
  });

  const onSubmit = async (values: CreateDepartment) => {
    try {
      const res = await fetch("/api/proxy/departments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: values.name,
          code: values.code.toUpperCase(),
          description: values.description || null,
          parentDepartmentId: values.parentDepartmentId || null,
          headUserId: values.headUserId || null,
        }),
      });
      const result = await res.json().catch(() => ({}));
      if (res.ok && result.isSuccess !== false) {
        toast.success("Office created successfully");
        reset();
        onOpenChange(false);
        onCreated?.();
      } else {
        toast.error(result.error || "Failed to create office");
      }
    } catch {
      toast.error("Failed to create office. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Create office</SheetTitle>
          <SheetDescription>Add a branch or office for this agency.</SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 mt-6 px-1">
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...register("name")} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="code">Code</Label>
            <Input
              id="code"
              placeholder="ADDIS-01"
              className="uppercase"
              {...register("code")}
            />
            {errors.code && (
              <p className="text-xs text-destructive">{errors.code.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="description">Description</Label>
            <Input id="description" {...register("description")} />
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
              {isSubmitting ? "Saving…" : "Create"}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
