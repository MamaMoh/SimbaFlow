"use client";

import { useEffect } from "react";
import { useForm, Controller } from "react-hook-form";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { WorkflowStage } from "@/types/workflow";
import { toast } from "sonner";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().max(500).optional().nullable(),
  sortOrder: z.coerce.number().int().min(0),
  stageType: z.coerce.number().int().min(0).max(2),
});

type FormValues = z.infer<typeof schema>;

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stage?: WorkflowStage | null;
  defaultSortOrder?: number;
  onSaved?: () => void;
};

const STAGE_TYPES = [
  { value: "0", label: "Simple" },
  { value: "1", label: "Parallel tracks" },
  { value: "2", label: "Milestone sequence" },
];

export function StageEditor({
  open,
  onOpenChange,
  stage,
  defaultSortOrder = 0,
  onSaved,
}: Props) {
  const isEdit = !!stage;
  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: "",
      description: "",
      sortOrder: defaultSortOrder,
      stageType: 0,
    },
  });

  useEffect(() => {
    if (open) {
      reset(
        stage
          ? {
              name: stage.name,
              description: stage.description ?? "",
              sortOrder: stage.sortOrder,
              stageType: stage.stageType,
            }
          : {
              name: "",
              description: "",
              sortOrder: defaultSortOrder,
              stageType: 0,
            }
      );
    }
  }, [open, stage, defaultSortOrder, reset]);

  const onSubmit = async (values: FormValues) => {
    try {
      const body = {
        name: values.name,
        description: values.description || null,
        sortOrder: values.sortOrder,
        stageType: values.stageType,
      };

      const res = await fetch(
        isEdit
          ? `/api/proxy/workflow/config/stages/${stage!.id}`
          : "/api/proxy/workflow/config/stages",
        {
          method: isEdit ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        }
      );
      const result = await res.json().catch(() => ({}));
      if (res.ok && result.isSuccess !== false) {
        toast.success(isEdit ? "Stage updated" : "Stage created");
        onOpenChange(false);
        onSaved?.();
      } else {
        toast.error(result.error || "Failed to save stage");
      }
    } catch {
      toast.error("Failed to save stage. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{isEdit ? "Edit stage" : "Add stage"}</SheetTitle>
          <SheetDescription>
            Configure a pipeline stage for this tenant workflow.
          </SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 mt-6 px-1">
          <div className="space-y-1.5">
            <Label htmlFor="stage-name">Name</Label>
            <Input id="stage-name" {...register("name")} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="stage-description">Description</Label>
            <Input id="stage-description" {...register("description")} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="stage-sort">Sort order</Label>
            <Input id="stage-sort" type="number" {...register("sortOrder")} />
            {errors.sortOrder && (
              <p className="text-xs text-destructive">{errors.sortOrder.message}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label>Stage type</Label>
            <Controller
              control={control}
              name="stageType"
              render={({ field }) => (
                <Select
                  value={String(field.value)}
                  onValueChange={(v) => field.onChange(Number(v))}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STAGE_TYPES.map((t) => (
                      <SelectItem key={t.value} value={t.value}>
                        {t.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
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
              {isSubmitting ? "Saving…" : isEdit ? "Save" : "Create"}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
