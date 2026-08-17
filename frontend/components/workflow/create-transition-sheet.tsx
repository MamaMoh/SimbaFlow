"use client";

import { useEffect, useState } from "react";
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
import {
  ConditionBuilder,
  emptyConditionGroup,
} from "@/components/workflow/condition-builder";
import type { ConditionGroup, WorkflowStage } from "@/types/workflow";
import { toast } from "sonner";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stages: WorkflowStage[];
  defaultSourceId?: string;
  onCreated?: () => void;
};

export function CreateTransitionSheet({
  open,
  onOpenChange,
  stages,
  defaultSourceId,
  onCreated,
}: Props) {
  const [sourceStageId, setSourceStageId] = useState("");
  const [targetStageId, setTargetStageId] = useState("");
  const [buttonLabel, setButtonLabel] = useState("");
  const [buttonIcon, setButtonIcon] = useState("");
  const [removeFromSource, setRemoveFromSource] = useState(true);
  const [requiredFields, setRequiredFields] = useState("");
  const [allowedRoles, setAllowedRoles] = useState("");
  const [conditions, setConditions] = useState<ConditionGroup>(emptyConditionGroup());
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setSourceStageId(defaultSourceId || stages[0]?.id || "");
      setTargetStageId(stages[1]?.id || stages[0]?.id || "");
      setButtonLabel("");
      setButtonIcon("");
      setRemoveFromSource(true);
      setRequiredFields("");
      setAllowedRoles("");
      setConditions(emptyConditionGroup());
    }
  }, [open, defaultSourceId, stages]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!sourceStageId || !targetStageId || !buttonLabel.trim()) {
      toast.error("Source, target, and button label are required");
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        sourceStageId,
        targetStageId,
        buttonLabel: buttonLabel.trim(),
        buttonIcon: buttonIcon.trim() || null,
        conditions:
          conditions.rules.length > 0
            ? conditions
            : { operator: "AND", rules: [] },
        requiredFields: requiredFields
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        allowedRoles: allowedRoles
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        removeFromSource,
      };

      const res = await fetch("/api/proxy/workflow/config/transitions", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const result = await res.json().catch(() => ({}));
      if (res.ok && result.isSuccess !== false) {
        toast.success("Transition created");
        onOpenChange(false);
        onCreated?.();
      } else {
        toast.error(result.error || "Failed to create transition");
      }
    } catch {
      toast.error("Failed to create transition. Please try again.");
    }
    setSubmitting(false);
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-lg overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Add transition</SheetTitle>
          <SheetDescription>
            Define a button that moves candidates between stages.
          </SheetDescription>
        </SheetHeader>
        <form onSubmit={onSubmit} className="space-y-4 mt-6 px-1">
          <div className="space-y-1.5">
            <Label>From stage</Label>
            <Select value={sourceStageId} onValueChange={setSourceStageId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select source" />
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
          <div className="space-y-1.5">
            <Label>To stage</Label>
            <Select value={targetStageId} onValueChange={setTargetStageId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select target" />
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
          <div className="space-y-1.5">
            <Label htmlFor="btn-label">Button label</Label>
            <Input
              id="btn-label"
              value={buttonLabel}
              onChange={(e) => setButtonLabel(e.target.value)}
              placeholder="To Embassy"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="btn-icon">Button icon (optional)</Label>
            <Input
              id="btn-icon"
              value={buttonIcon}
              onChange={(e) => setButtonIcon(e.target.value)}
              placeholder="arrow-right"
            />
          </div>
          <ConditionBuilder value={conditions} onChange={setConditions} />
          <div className="space-y-1.5">
            <Label htmlFor="req-fields">Required fields (comma-separated)</Label>
            <Input
              id="req-fields"
              value={requiredFields}
              onChange={(e) => setRequiredFields(e.target.value)}
              placeholder="passportNumber, labourId"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="roles">Allowed roles (comma-separated)</Label>
            <Input
              id="roles"
              value={allowedRoles}
              onChange={(e) => setAllowedRoles(e.target.value)}
              placeholder="AgencyAdmin, Operations"
            />
          </div>
          <div className="flex items-center gap-2">
            <Checkbox
              id="remove-source"
              checked={removeFromSource}
              onCheckedChange={(v) => setRemoveFromSource(v === true)}
            />
            <Label htmlFor="remove-source" className="font-normal">
              Remove candidate from source stage
            </Label>
          </div>
          <SheetFooter className="pt-4">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={submitting}
              className="bg-green-800 hover:bg-green-900"
            >
              {submitting ? "Saving…" : "Create"}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
