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
              stageType: stage.stageType,
            }
          : { name: "", description: "", stageType: 0 }
      );
    }
  }, [open, stage, defaultSortOrder, reset]);

  const onSubmit = async (values: FormValues) => {
    try {
      const body = {
        name: values.name,
        description: values.description || null,
        // Not asked for: a new step goes on the end, and an existing one keeps where it
        // is. The list reorders itself with Move up and Move down, which renumbers the whole
        // pipeline at once — see ReorderStagesCommand.
        sortOrder: stage?.sortOrder ?? defaultSortOrder,
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
        toast.success(isEdit ? "Step saved" : "Step added");
        onOpenChange(false);
        onSaved?.();
      } else {
        toast.error(result.error || "Could not save the step");
      }
    } catch {
      toast.error("Could not save the step. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent aria-describedby={undefined} className="sm:max-w-md overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{isEdit ? "Edit step" : "Add step"}</SheetTitle>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="mt-2 space-y-4 px-6">
          <div className="space-y-1.5">
            <Label htmlFor="stage-name">Name</Label>
            <Input id="stage-name" {...register("name")} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
            {isEdit && (
              <p className="text-xs text-muted-foreground">
                The name is how this step is found: the board in the sidebar, its address, and
                what the bot calls it. Renaming one of the steps the app ships with — Embassy,
                LMIS, Ticket — gives it the general-purpose board instead of its own.
              </p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="stage-description">Description</Label>
            <Input id="stage-description" {...register("description")} />
            <p className="text-xs text-muted-foreground">
              Shown under the name in the setup list. Optional.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label>Kind</Label>
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
            <p className="text-xs text-muted-foreground">
              Simple is one status at a time. Parallel runs several side by side, the way
              Embassy runs medical, tasheer and visa. Milestones go in a fixed order.
            </p>
          </div>
          <SheetFooter className="-mx-6 mt-2">
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
