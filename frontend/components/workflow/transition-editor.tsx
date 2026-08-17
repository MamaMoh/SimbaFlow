"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
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
import type { WorkflowTransitionRule } from "@/types/workflow";

/** Roles an agency can assign to a step. */
export const ASSIGNABLE_ROLES = [
  "AgencyOwner",
  "OfficeManager",
  "EmbassyOfficer",
  "CaseExecutive",
  "FinanceOfficer",
  "FieldAgent",
  "DataEntryClerk",
  "Auditor",
] as const;

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transition: WorkflowTransitionRule | null;
  fromName?: string;
  toName?: string;
  onSaved?: () => void;
};

/**
 * Edits one workflow step: its button label and — the important part — WHICH
 * ROLES may perform it. Leaving every role unchecked means "anyone with access",
 * so one agency can let a single person do register + contract while another
 * splits those steps across two roles.
 */
export function TransitionEditor({
  open,
  onOpenChange,
  transition,
  fromName,
  toName,
  onSaved,
}: Props) {
  const [label, setLabel] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!transition) return;
    setLabel(transition.buttonLabel ?? "");
    setRoles(transition.allowedRoles ?? []);
    setIsActive(transition.isActive ?? true);
  }, [transition]);

  const toggleRole = (role: string) =>
    setRoles((cur) =>
      cur.includes(role) ? cur.filter((r) => r !== role) : [...cur, role],
    );

  const save = async () => {
    if (!transition) return;
    if (!label.trim()) {
      toast.error("Button label is required");
      return;
    }
    setSaving(true);
    try {
      const res = await fetch(
        `/api/proxy/workflow/config/transitions/${transition.id}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            buttonLabel: label.trim(),
            buttonIcon: transition.buttonIcon ?? null,
            requiredFields: transition.requiredFields ?? [],
            allowedRoles: roles,
            removeFromSource: transition.removeFromSource ?? true,
            isActive,
          }),
        },
      );
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) {
        throw new Error(body?.error || "Failed to save step");
      }
      toast.success("Step updated");
      onOpenChange(false);
      onSaved?.();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to save step");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full sm:max-w-md overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Edit step</SheetTitle>
          <SheetDescription>
            {fromName && toName ? `${fromName} → ${toName}` : "Workflow step"}
          </SheetDescription>
        </SheetHeader>

        <div className="mt-6 space-y-5 px-1">
          <div className="space-y-1.5">
            <Label htmlFor="t-label">Button text</Label>
            <Input
              id="t-label"
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="e.g. To Embassy"
            />
            <p className="text-xs text-muted-foreground">
              What staff see on the button for this step.
            </p>
          </div>

          <div className="space-y-2">
            <Label>Who can do this step?</Label>
            <p className="text-xs text-muted-foreground">
              Tick the roles allowed to perform it. Leave all unticked to allow
              anyone with access to this stage.
            </p>
            <div className="grid grid-cols-2 gap-2 pt-1">
              {ASSIGNABLE_ROLES.map((role) => (
                <label
                  key={role}
                  className="flex cursor-pointer items-center gap-2 rounded-md border p-2 text-sm hover:bg-muted/50"
                >
                  <Checkbox
                    checked={roles.includes(role)}
                    onCheckedChange={() => toggleRole(role)}
                  />
                  <span>{role.replace(/([A-Z])/g, " $1").trim()}</span>
                </label>
              ))}
            </div>
          </div>

          <label className="flex cursor-pointer items-center gap-2 rounded-md border p-3 text-sm">
            <Checkbox
              checked={isActive}
              onCheckedChange={(v) => setIsActive(v === true)}
            />
            <span>
              Step is active
              <span className="block text-xs text-muted-foreground">
                Turn off to hide this button without deleting the step.
              </span>
            </span>
          </label>
        </div>

        <SheetFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving}>
            {saving ? "Saving…" : "Save step"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
