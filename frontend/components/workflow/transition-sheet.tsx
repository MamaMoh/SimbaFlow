"use client";

import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Switch } from "@/components/ui/switch";
import {
  Sheet,
  SheetContent,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  ConditionBuilder,
  emptyConditionGroup,
} from "@/components/workflow/condition-builder";
import { conditionFields, humaniseField } from "@/lib/workflow/condition-fields";
import type {
  ConditionGroup,
  WorkflowStage,
  WorkflowTransitionRule,
} from "@/types/workflow";
import { Loader2, X } from "lucide-react";

/** Roles an agency can put a step behind. */
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
  /** null = adding a step. */
  transition: WorkflowTransitionRule | null;
  stages: WorkflowStage[];
  /** Pre-selects the board a step is being added from. */
  defaultSourceId?: string;
  onSaved: () => void;
};

/**
 * One sheet for adding a step and for editing one.
 *
 * They were two, and they disagreed: adding let you build conditions and typed the roles into a
 * comma-separated box, editing ticked roles from a list and could not reach the conditions at
 * all. Which meant a condition set once could never be corrected — the only way to change one
 * was to delete the step and build it again, losing the step's history with it.
 */
export function TransitionSheet({
  open,
  onOpenChange,
  transition,
  stages,
  defaultSourceId,
  onSaved,
}: Props) {
  const isEdit = !!transition;

  const [sourceStageId, setSourceStageId] = useState("");
  const [targetStageId, setTargetStageId] = useState("");
  const [label, setLabel] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [requiredFields, setRequiredFields] = useState<string[]>([]);
  const [conditions, setConditions] = useState<ConditionGroup>(emptyConditionGroup());
  const [removeFromSource, setRemoveFromSource] = useState(true);
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setSourceStageId(transition?.sourceStageId ?? defaultSourceId ?? stages[0]?.id ?? "");
    setTargetStageId(transition?.targetStageId ?? "");
    setLabel(transition?.buttonLabel ?? "");
    setRoles(transition?.allowedRoles ?? []);
    setRequiredFields(transition?.requiredFields ?? []);
    setConditions(
      transition?.conditions?.rules?.length ? transition.conditions : emptyConditionGroup(),
    );
    setRemoveFromSource(transition?.removeFromSource ?? true);
    setIsActive(transition?.isActive ?? true);
  }, [open, transition, defaultSourceId, stages]);

  const source = stages.find((s) => s.id === sourceStageId);
  const fields = useMemo(() => conditionFields(stages, source), [stages, source]);

  const toggleRole = (role: string) =>
    setRoles((r) => (r.includes(role) ? r.filter((x) => x !== role) : [...r, role]));

  const save = async () => {
    if (!label.trim()) {
      toast.error("The button needs something written on it");
      return;
    }
    if (!isEdit && (!sourceStageId || !targetStageId)) {
      toast.error("Pick the step it goes from and the step it goes to");
      return;
    }
    if (!isEdit && sourceStageId === targetStageId) {
      toast.error("A step cannot lead back to where it starts");
      return;
    }

    setSaving(true);
    try {
      const body = isEdit
        ? {
            buttonLabel: label.trim(),
            buttonIcon: transition!.buttonIcon ?? null,
            requiredFields,
            allowedRoles: roles,
            removeFromSource,
            isActive,
            conditions,
          }
        : {
            sourceStageId,
            targetStageId,
            buttonLabel: label.trim(),
            buttonIcon: null,
            conditions,
            requiredFields,
            allowedRoles: roles,
            removeFromSource,
          };

      const res = await fetch(
        isEdit
          ? `/api/proxy/workflow/config/transitions/${transition!.id}`
          : "/api/proxy/workflow/config/transitions",
        {
          method: isEdit ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        },
      );
      const result = await res.json().catch(() => ({}));
      if (!res.ok || result?.isSuccess === false) {
        throw new Error(result?.error || "Could not save the move");
      }
      toast.success(isEdit ? "Move saved" : "Move added");
      onSaved();
      onOpenChange(false);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not save the move");
    } finally {
      setSaving(false);
    }
  };

  const target = stages.find((s) => s.id === targetStageId);

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        aria-describedby={undefined}
        className="w-full overflow-y-auto sm:max-w-xl"
      >
        <SheetHeader>
          <SheetTitle>{isEdit ? "Edit move" : "Add move"}</SheetTitle>
        </SheetHeader>

        <div className="mt-2 space-y-6 px-6 pb-4">
          <section className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>From</Label>
                {isEdit ? (
                  <p className="flex h-9 items-center text-sm">{source?.name ?? "—"}</p>
                ) : (
                  <Select value={sourceStageId} onValueChange={setSourceStageId}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Choose a step" />
                    </SelectTrigger>
                    <SelectContent>
                      {stages.map((s) => (
                        <SelectItem key={s.id} value={s.id}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
              <div className="space-y-1.5">
                <Label>To</Label>
                {isEdit ? (
                  <p className="flex h-9 items-center text-sm">{target?.name ?? "—"}</p>
                ) : (
                  <Select value={targetStageId} onValueChange={setTargetStageId}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Choose a step" />
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
                )}
              </div>
            </div>
            {isEdit && (
              <p className="text-xs text-muted-foreground">
                Where a move goes cannot be changed — candidates have already travelled it, and
                the record of those moves names this one. Remove it and add another instead.
              </p>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="t-label">Button text</Label>
              <Input
                id="t-label"
                value={label}
                onChange={(e) => setLabel(e.target.value)}
                placeholder="To Embassy"
              />
              <p className="text-xs text-muted-foreground">
                What staff see in the row menu.
              </p>
            </div>
          </section>

          <section className="space-y-2">
            <Label>Who can do it</Label>
            <p className="text-xs text-muted-foreground">
              Leave all of them unticked to allow anyone who can reach the board.
            </p>
            <div className="grid grid-cols-1 gap-2 pt-1 sm:grid-cols-2">
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
          </section>

          <section className="space-y-2">
            <Label>When it is offered</Label>
            <p className="text-xs text-muted-foreground">
              The button only appears on a candidate whose record matches. Leave it empty and the
              step is always offered.
            </p>
            <ConditionBuilder
              value={conditions}
              onChange={setConditions}
              stages={stages}
              focus={source}
            />
          </section>

          <section className="space-y-2">
            <Label>Must be filled in first</Label>
            <p className="text-xs text-muted-foreground">
              The step is refused, with the missing ones named, until the record has them.
            </p>
            <FieldPicker
              selected={requiredFields}
              onChange={setRequiredFields}
              fields={fields}
            />
          </section>

          <section className="space-y-3">
            <div className="flex items-center justify-between gap-4 rounded-lg border p-3">
              <div>
                <p className="text-sm font-medium">Take them off this board</p>
                <p className="text-xs text-muted-foreground">
                  On: they move to the next board. Off: they appear on both, which is what the
                  Arrival ledger needs so the record of who has travelled is not emptied.
                </p>
              </div>
              <Switch checked={removeFromSource} onCheckedChange={setRemoveFromSource} />
            </div>

            {isEdit && (
              <div className="flex items-center justify-between gap-4 rounded-lg border p-3">
                <div>
                  <p className="text-sm font-medium">Active</p>
                  <p className="text-xs text-muted-foreground">
                    Turn off to hide the button without removing the step.
                  </p>
                </div>
                <Switch checked={isActive} onCheckedChange={setIsActive} />
              </div>
            )}
          </section>
        </div>

        <SheetFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button
            onClick={() => void save()}
            disabled={saving}
            className="bg-green-800 text-white hover:bg-green-900"
          >
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            {saving ? "Saving…" : isEdit ? "Save move" : "Add move"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

/**
 * Pick several fields from the same list the conditions use.
 *
 * This was a comma-separated text box, which required knowing that the passport field is called
 * `passportNumber` and not `passport`. A name that does not exist is never filled in, so the
 * step it guards can never be taken, and nothing says why.
 */
function FieldPicker({
  selected,
  onChange,
  fields,
}: {
  selected: string[];
  onChange: (next: string[]) => void;
  fields: ReturnType<typeof conditionFields>;
}) {
  const groups = ["This step", "Other steps", "The candidate"] as const;
  const remaining = fields.filter((f) => !selected.includes(f.value));

  return (
    <div className="space-y-2">
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((f) => (
            <span
              key={f}
              className="inline-flex items-center gap-1 rounded-full border bg-background px-2 py-0.5 text-xs"
            >
              {humaniseField(f)}
              <button
                type="button"
                aria-label={`Remove ${humaniseField(f)}`}
                className="text-muted-foreground hover:text-destructive"
                onClick={() => onChange(selected.filter((x) => x !== f))}
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
        </div>
      )}
      <Select value="" onValueChange={(v) => v && onChange([...selected, v])}>
        <SelectTrigger className="w-full">
          <SelectValue placeholder={selected.length ? "Add another…" : "Nothing required"} />
        </SelectTrigger>
        <SelectContent className="max-h-72">
          {groups.map((g) => {
            const inGroup = remaining.filter((f) => f.group === g);
            if (inGroup.length === 0) return null;
            return (
              <SelectGroup key={g}>
                <SelectLabel>{g}</SelectLabel>
                {inGroup.map((f) => (
                  <SelectItem key={f.value} value={f.value}>
                    {f.label}
                  </SelectItem>
                ))}
              </SelectGroup>
            );
          })}
        </SelectContent>
      </Select>
    </div>
  );
}
