"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { ConditionBuilder, emptyConditionGroup } from "@/components/workflow/condition-builder";
import type { ConditionGroup, MirrorViewRule, WorkflowStage } from "@/types/workflow";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";

/**
 * Create or retune a mirror rule.
 *
 * Editing a rule re-runs it across every candidate in flight, so relaxing "tasheer must be
 * booked" immediately pulls the waiting candidates onto the LMIS board instead of leaving
 * them stranded until someone touches each record.
 */
export function MirrorRuleEditor({
  open,
  onOpenChange,
  rule,
  stages,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** null = creating a new mirror */
  rule: MirrorViewRule | null;
  stages: WorkflowStage[];
  onSaved: () => void;
}) {
  const [sourceStageId, setSourceStageId] = useState("");
  const [targetStageId, setTargetStageId] = useState("");
  const [conditions, setConditions] = useState<ConditionGroup>(emptyConditionGroup());
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setSourceStageId(rule?.sourceStageId ?? "");
    setTargetStageId(rule?.targetStageId ?? "");
    setConditions(
      rule?.conditions?.rules?.length ? rule.conditions : emptyConditionGroup()
    );
    setIsActive(rule?.isActive ?? true);
  }, [open, rule]);

  const save = async () => {
    if (!sourceStageId || !targetStageId) {
      toast.error("Pick both a source and a target step");
      return;
    }
    if (sourceStageId === targetStageId) {
      toast.error("A step cannot mirror into itself");
      return;
    }

    setSaving(true);
    try {
      const res = await fetch("/api/proxy/workflow/config/mirrors", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: rule?.id ?? null,
          sourceStageId,
          targetStageId,
          conditions,
          isActive,
        }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) {
        throw new Error(body?.error || "Failed to save mirror");
      }
      toast.success(rule ? "Mirror updated and re-applied" : "Mirror created and applied");
      onSaved();
      onOpenChange(false);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to save mirror");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
        <SheetHeader>
          <SheetTitle>{rule ? "Edit mirror" : "New mirror"}</SheetTitle>
          <SheetDescription>
            Show a candidate on a second board while they stay on the first. Saving re-checks
            every candidate currently in flight against the new conditions.
          </SheetDescription>
        </SheetHeader>

        <div className="mt-6 space-y-6">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>From board</Label>
              <Select value={sourceStageId} onValueChange={setSourceStageId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select step" />
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
              <Label>Also appears on</Label>
              <Select value={targetStageId} onValueChange={setTargetStageId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select step" />
                </SelectTrigger>
                <SelectContent>
                  {stages
                    .filter((s) => s.id !== sourceStageId)
                    .map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>Conditions</Label>
            <p className="text-xs text-muted-foreground">
              The candidate appears on the second board once these are met. Leave empty to
              mirror everyone. Values are matched ignoring case.
            </p>
            <ConditionBuilder value={conditions} onChange={setConditions} />
          </div>

          <div className="flex items-center justify-between rounded-lg border p-3">
            <div>
              <p className="text-sm font-medium">Active</p>
              <p className="text-xs text-muted-foreground">
                Turn off to stop mirroring without deleting the rule.
              </p>
            </div>
            <Switch checked={isActive} onCheckedChange={setIsActive} />
          </div>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={() => void save()} disabled={saving}>
              {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              {saving ? "Applying…" : "Save & apply"}
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  );
}
