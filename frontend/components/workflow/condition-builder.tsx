"use client";

import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { conditionFields, describeConditions, type FieldOption } from "@/lib/workflow/condition-fields";
import type { ConditionGroup, ConditionRule, WorkflowStage } from "@/types/workflow";
import { Plus, Trash2 } from "lucide-react";

const OPS: { value: ConditionRule["op"]; label: string; needsValue: boolean }[] = [
  { value: "eq", label: "is", needsValue: true },
  { value: "neq", label: "is not", needsValue: true },
  { value: "in", label: "is any of", needsValue: true },
  { value: "not_empty", label: "is filled in", needsValue: false },
  { value: "empty", label: "is empty", needsValue: false },
];

/** Picked from the list, or typed by hand — the escape hatch for a field only the agency knows. */
const SOMETHING_ELSE = "__other__";

type Props = {
  value: ConditionGroup;
  onChange: (next: ConditionGroup) => void;
  /** The agency's pipeline, which is where the fields and their values come from. */
  stages: readonly WorkflowStage[];
  /** The step this condition hangs off, so its own tracks are offered first. */
  focus?: WorkflowStage;
  /**
   * What no conditions at all means here. A move with none is always offered; a mirror with
   * none pulls everybody across. Same control, two different sentences.
   */
  emptyMeans?: string;
};

export function emptyConditionGroup(): ConditionGroup {
  return { operator: "AND", rules: [] };
}

export function ConditionBuilder({
  value,
  onChange,
  stages,
  focus,
  emptyMeans = "always offered, subject to who may do it and what has to be filled in",
}: Props) {
  const fields = useMemo(() => conditionFields(stages, focus), [stages, focus]);

  // Which rows the person has chosen to type a field into rather than pick one. Kept here
  // rather than on the rule, because it is about this editing session and not about the rule.
  const [typing, setTyping] = useState<Record<number, boolean>>({});

  const updateRule = (index: number, patch: Partial<ConditionRule>) =>
    onChange({
      ...value,
      rules: value.rules.map((r, i) => (i === index ? { ...r, ...patch } : r)),
    });

  const removeRule = (index: number) => {
    onChange({ ...value, rules: value.rules.filter((_, i) => i !== index) });
    setTyping((t) => {
      const next: Record<number, boolean> = {};
      for (const [k, v] of Object.entries(t)) {
        const i = Number(k);
        if (i < index) next[i] = v;
        else if (i > index) next[i - 1] = v;
      }
      return next;
    });
  };

  const addRule = () =>
    onChange({ ...value, rules: [...value.rules, { field: "", op: "eq", value: "" }] });

  const byValue = (field: string): FieldOption | undefined =>
    fields.find((f) => f.value === field);

  const groups = ["This step", "Other steps", "The candidate"] as const;

  return (
    <div className="space-y-3 rounded-md border bg-muted/20 p-3">
      {value.rules.length > 1 && (
        <div className="flex items-center gap-2">
          <Label className="text-xs text-muted-foreground">Match</Label>
          <Select
            value={value.operator}
            onValueChange={(op) =>
              onChange({ ...value, operator: op as ConditionGroup["operator"] })
            }
          >
            <SelectTrigger size="sm" className="w-[150px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="AND">all of these</SelectItem>
              <SelectItem value="OR">any of these</SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}

      {value.rules.length === 0 && (
        <p className="text-xs text-muted-foreground">No conditions — {emptyMeans}.</p>
      )}

      {value.rules.map((rule, index) => {
        const option = byValue(rule.field);
        const op = OPS.find((o) => o.value === rule.op) ?? OPS[0];
        const typeIt = typing[index] || (!!rule.field && !option);

        return (
          <div key={index} className="grid gap-2 sm:grid-cols-[1.1fr_auto_1.1fr_auto] sm:items-end">
            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">Field</Label>
              {typeIt ? (
                <Input
                  autoFocus={typing[index]}
                  placeholder="track or field name"
                  value={rule.field}
                  onChange={(e) => updateRule(index, { field: e.target.value })}
                />
              ) : (
                <Select
                  value={rule.field || undefined}
                  onValueChange={(v) => {
                    if (v === SOMETHING_ELSE) {
                      setTyping((t) => ({ ...t, [index]: true }));
                      updateRule(index, { field: "" });
                      return;
                    }
                    updateRule(index, { field: v, value: "" });
                  }}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder="Choose a field" />
                  </SelectTrigger>
                  <SelectContent className="max-h-72">
                    {groups.map((g) => {
                      const inGroup = fields.filter((f) => f.group === g);
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
                    <SelectGroup>
                      <SelectItem value={SOMETHING_ELSE}>Something else…</SelectItem>
                    </SelectGroup>
                  </SelectContent>
                </Select>
              )}
            </div>

            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">Test</Label>
              <Select
                value={rule.op}
                onValueChange={(v) =>
                  updateRule(index, { op: v as ConditionRule["op"], value: "" })
                }
              >
                <SelectTrigger className="w-full sm:w-[130px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {OPS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1">
              <Label className="text-xs text-muted-foreground">
                {op.needsValue ? "Value" : " "}
              </Label>
              {!op.needsValue ? (
                <div className="h-9" />
              ) : rule.op !== "in" && option?.options?.length ? (
                <Select
                  value={(rule.value as string) || undefined}
                  onValueChange={(v) => updateRule(index, { value: v })}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder="Choose a value" />
                  </SelectTrigger>
                  <SelectContent>
                    {option.options.map((v) => (
                      <SelectItem key={v} value={v}>
                        {v}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input
                  placeholder={rule.op === "in" ? "Fit, Unfit" : "Ready"}
                  value={Array.isArray(rule.value) ? rule.value.join(", ") : (rule.value ?? "")}
                  onChange={(e) =>
                    updateRule(index, {
                      value:
                        rule.op === "in"
                          ? e.target.value.split(",").map((s) => s.trim()).filter(Boolean)
                          : e.target.value,
                    })
                  }
                />
              )}
            </div>

            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label="Remove condition"
              className="h-9 w-9 text-destructive"
              onClick={() => removeRule(index)}
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        );
      })}

      <div className="flex flex-wrap items-center justify-between gap-2">
        <Button type="button" variant="outline" size="sm" onClick={addRule}>
          <Plus className="mr-1 h-4 w-4" /> Add condition
        </Button>
        {/* The rules read back as a sentence. Three dropdowns are easy to fill in and hard to
            re-read, and this is the line that catches an AND that should have been an OR. */}
        {value.rules.length > 0 && (
          <p className="text-xs text-muted-foreground">
            Offered when <span className="font-medium">{describeConditions(value)}</span>
          </p>
        )}
      </div>
    </div>
  );
}
