"use client";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { ConditionGroup, ConditionRule } from "@/types/workflow";
import { Plus, Trash2 } from "lucide-react";

const OPS: { value: ConditionRule["op"]; label: string }[] = [
  { value: "eq", label: "equals" },
  { value: "neq", label: "not equals" },
  { value: "not_empty", label: "is not empty" },
  { value: "empty", label: "is empty" },
  { value: "in", label: "in list" },
];

type Props = {
  value: ConditionGroup;
  onChange: (next: ConditionGroup) => void;
};

export function emptyConditionGroup(): ConditionGroup {
  return { operator: "AND", rules: [] };
}

export function ConditionBuilder({ value, onChange }: Props) {
  const updateRule = (index: number, patch: Partial<ConditionRule>) => {
    const rules = value.rules.map((r, i) => (i === index ? { ...r, ...patch } : r));
    onChange({ ...value, rules });
  };

  const removeRule = (index: number) => {
    onChange({ ...value, rules: value.rules.filter((_, i) => i !== index) });
  };

  const addRule = () => {
    onChange({
      ...value,
      rules: [...value.rules, { field: "", op: "eq", value: "" }],
    });
  };

  return (
    <div className="space-y-3 rounded-md border p-3">
      <div className="flex items-center justify-between gap-2">
        <Label className="text-xs text-muted-foreground">Conditions</Label>
        <Select
          value={value.operator}
          onValueChange={(op) =>
            onChange({ ...value, operator: op as ConditionGroup["operator"] })
          }
        >
          <SelectTrigger size="sm" className="w-[100px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="AND">AND</SelectItem>
            <SelectItem value="OR">OR</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {value.rules.length === 0 && (
        <p className="text-xs text-muted-foreground">
          No conditions — transition is always available (subject to roles/fields).
        </p>
      )}

      {value.rules.map((rule, index) => (
        <div key={index} className="grid grid-cols-[1fr_1fr_1fr_auto] gap-2 items-end">
          <div className="space-y-1">
            <Label className="text-xs">Field</Label>
            <Input
              placeholder="status"
              value={rule.field}
              onChange={(e) => updateRule(index, { field: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Operator</Label>
            <Select
              value={rule.op}
              onValueChange={(op) =>
                updateRule(index, { op: op as ConditionRule["op"] })
              }
            >
              <SelectTrigger className="w-full">
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
            <Label className="text-xs">Value</Label>
            <Input
              placeholder="Ready"
              disabled={rule.op === "empty" || rule.op === "not_empty"}
              value={
                Array.isArray(rule.value)
                  ? rule.value.join(",")
                  : (rule.value ?? "")
              }
              onChange={(e) => {
                if (rule.op === "in") {
                  updateRule(index, {
                    value: e.target.value
                      .split(",")
                      .map((s) => s.trim())
                      .filter(Boolean),
                  });
                } else {
                  updateRule(index, { value: e.target.value });
                }
              }}
            />
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-9 w-9 text-destructive"
            onClick={() => removeRule(index)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ))}

      <Button type="button" variant="outline" size="sm" onClick={addRule}>
        <Plus className="h-4 w-4 mr-1" /> Add rule
      </Button>
    </div>
  );
}
