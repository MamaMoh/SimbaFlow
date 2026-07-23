"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowRightLeft } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { WorkflowStage, WorkflowTransitionRule } from "@/types/workflow";
import { upsertTransition } from "@/lib/demo/workflow-config-store";

const schema = z
  .object({
    buttonLabel: z.string().min(2, "Button label is required"),
    sourceStageId: z.string().min(1, "Source stage required"),
    targetStageId: z.string().min(1, "Target stage required"),
    allowedRoles: z.string().optional(),
    requiredFields: z.string().optional(),
    conditionField: z.string().optional(),
    conditionValue: z.string().optional(),
    removeFromSource: z.boolean(),
    isActive: z.boolean(),
  })
  .refine((d) => d.sourceStageId !== d.targetStageId, {
    message: "Source and target must differ",
    path: ["targetStageId"],
  });

type FormValues = z.infer<typeof schema>;

export function TransitionFormSheet({
  open,
  onOpenChange,
  stages,
  transition,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stages: WorkflowStage[];
  transition?: WorkflowTransitionRule | null;
  onSaved: () => void;
}) {
  const isEdit = !!transition;

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      buttonLabel: "",
      sourceStageId: "",
      targetStageId: "",
      allowedRoles: "CaseExecutive, Admin",
      requiredFields: "",
      conditionField: "",
      conditionValue: "",
      removeFromSource: true,
      isActive: true,
    },
  });

  const sourceStageId = watch("sourceStageId");
  const targetStageId = watch("targetStageId");
  const removeFromSource = watch("removeFromSource");
  const isActive = watch("isActive");

  useEffect(() => {
    if (!open) return;
    if (transition) {
      const rule = transition.conditions?.rules?.[0];
      reset({
        buttonLabel: transition.buttonLabel,
        sourceStageId: transition.sourceStageId,
        targetStageId: transition.targetStageId,
        allowedRoles: transition.allowedRoles.join(", "),
        requiredFields: transition.requiredFields.join(", "),
        conditionField: typeof rule?.field === "string" ? rule.field : "",
        conditionValue: typeof rule?.value === "string" ? rule.value : "",
        removeFromSource: transition.removeFromSource,
        isActive: transition.isActive,
      });
    } else {
      reset({
        buttonLabel: "",
        sourceStageId: stages[0]?.id ?? "",
        targetStageId: stages[1]?.id ?? "",
        allowedRoles: "CaseExecutive, Admin",
        requiredFields: "",
        conditionField: "",
        conditionValue: "",
        removeFromSource: true,
        isActive: true,
      });
    }
  }, [open, transition, stages, reset]);

  const onSubmit = async (data: FormValues) => {
    await new Promise((r) => setTimeout(r, 250));
    upsertTransition({
      id: transition?.id,
      buttonLabel: data.buttonLabel,
      sourceStageId: data.sourceStageId,
      targetStageId: data.targetStageId,
      buttonIcon: transition?.buttonIcon,
      allowedRoles: data.allowedRoles
        ? data.allowedRoles.split(",").map((s) => s.trim()).filter(Boolean)
        : [],
      requiredFields: data.requiredFields
        ? data.requiredFields.split(",").map((s) => s.trim()).filter(Boolean)
        : [],
      conditionField: data.conditionField,
      conditionValue: data.conditionValue,
      removeFromSource: data.removeFromSource,
      isActive: data.isActive,
    });
    toast.success(isEdit ? "Transition updated" : "Transition created");
    onOpenChange(false);
    onSaved();
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col px-6 sm:max-w-[520px]">
        <SheetHeader className="pb-2">
          <SheetTitle className="flex items-center gap-2">
            <ArrowRightLeft className="h-5 w-5 text-primary" />
            {isEdit ? "Edit transition" : "Add transition"}
          </SheetTitle>
          <SheetDescription>
            Define the action button, allowed roles, and move conditions.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-1 flex-col gap-4 overflow-y-auto pb-6">
          <div className="space-y-2">
            <Label>Button label</Label>
            <Input placeholder="e.g. To Embassy" {...register("buttonLabel")} />
            {errors.buttonLabel && <p className="text-xs text-destructive">{errors.buttonLabel.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>From stage</Label>
              <Select value={sourceStageId} onValueChange={(v) => setValue("sourceStageId", v, { shouldValidate: true })}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Source" />
                </SelectTrigger>
                <SelectContent>
                  {stages.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>To stage</Label>
              <Select value={targetStageId} onValueChange={(v) => setValue("targetStageId", v, { shouldValidate: true })}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Target" />
                </SelectTrigger>
                <SelectContent>
                  {stages.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {errors.targetStageId && (
                <p className="text-xs text-destructive">{errors.targetStageId.message}</p>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label>Allowed roles (comma-separated)</Label>
            <Input placeholder="CaseExecutive, Admin" {...register("allowedRoles")} />
          </div>

          <div className="space-y-2">
            <Label>Required fields (comma-separated)</Label>
            <Input placeholder="passportNumber, flightDate" {...register("requiredFields")} />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Condition field</Label>
              <Input placeholder="embassy" {...register("conditionField")} />
            </div>
            <div className="space-y-2">
              <Label>Must equal</Label>
              <Input placeholder="Issued" {...register("conditionValue")} />
            </div>
          </div>

          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Remove from source</div>
              <div className="text-xs text-muted-foreground">Hide from previous stage after move</div>
            </div>
            <Switch checked={removeFromSource} onCheckedChange={(v) => setValue("removeFromSource", v)} />
          </div>

          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Active</div>
              <div className="text-xs text-muted-foreground">Show action button in stage workbench</div>
            </div>
            <Switch checked={isActive} onCheckedChange={(v) => setValue("isActive", v)} />
          </div>

          <div className="mt-auto flex gap-2 pt-4">
            <Button type="button" variant="outline" className="flex-1" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" className="flex-1" disabled={isSubmitting}>
              {isSubmitting ? "Saving…" : isEdit ? "Save changes" : "Create transition"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
