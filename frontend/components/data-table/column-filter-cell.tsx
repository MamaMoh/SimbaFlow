"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";
import { Filter, X } from "lucide-react";
import {
  defaultOperator,
  describe,
  isComplete,
  needsSecondValue,
  needsValue,
  operatorLabel,
  operatorsFor,
  type ColumnFilter,
  type ColumnFilterSpec,
  type FilterOperator,
} from "@/lib/data-table/column-filters";

/**
 * The funnel under one heading.
 *
 * Only on columns the server will actually filter on. A column whose value is computed, or
 * comes from somewhere other than the row, gets nothing here rather than a control that looks
 * like it works — a filter that silently does nothing is worse than no filter, because the
 * empty result reads as an answer.
 *
 * The popover holds the operator and the value together. Keeping the operator out of sight
 * behind a second click would make "greater than" harder to reach than typing, and a list of
 * ages is mostly asked about in ranges.
 */
export function ColumnFilterCell({
  spec,
  heading,
  value,
  onChange,
}: {
  spec: ColumnFilterSpec;
  heading: string;
  value: ColumnFilter | undefined;
  onChange: (next: ColumnFilter | null) => void;
}) {
  const [open, setOpen] = React.useState(false);

  // Edited here and handed over on close or on Enter, not on every keystroke. Each change is a
  // request to the server, and sending one per letter is how a list flickers.
  const [draft, setDraft] = React.useState<ColumnFilter>(
    value ?? { field: spec.field, op: defaultOperator(spec.kind) },
  );

  React.useEffect(() => {
    if (open) {
      setDraft(value ?? { field: spec.field, op: defaultOperator(spec.kind) });
    }
  }, [open, value, spec.field, spec.kind]);

  const apply = (next: ColumnFilter) => {
    onChange(isComplete(next) ? next : null);
  };

  const commit = () => {
    apply(draft);
    setOpen(false);
  };

  const set = (patch: Partial<ColumnFilter>) => setDraft((d) => ({ ...d, ...patch }));

  const active = !!value;
  const inputType = spec.kind === "date" ? "date" : spec.kind === "number" ? "number" : "text";

  return (
    <div className="flex items-center justify-start">
      <Popover
        open={open}
        onOpenChange={(next) => {
          // Closing by clicking away keeps what was typed, the way leaving a form field does.
          if (!next) apply(draft);
          setOpen(next);
        }}
      >
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={
              active ? describe(value, heading, spec.kind) : `Filter ${heading}`
            }
            title={active ? describe(value, heading, spec.kind) : `Filter ${heading}`}
            className={cn(
              "h-6 w-6",
              active && "text-primary bg-primary/10 hover:bg-primary/15",
            )}
          >
            <Filter className={cn("h-3.5 w-3.5", active && "fill-current")} />
          </Button>
        </PopoverTrigger>

        <PopoverContent align="start" className="w-[260px] space-y-2 p-2">
          <Select
            value={draft.op}
            onValueChange={(op) => {
              const next = op as FilterOperator;
              const patched: ColumnFilter = { ...draft, op: next };
              // Values that the new operator has no box for are dropped, so a leftover second
              // bound cannot travel with a filter that is no longer a range.
              if (!needsValue(next)) {
                patched.value = undefined;
                patched.value2 = undefined;
              } else if (!needsSecondValue(next)) {
                patched.value2 = undefined;
              }
              setDraft(patched);
              if (!needsValue(next)) {
                apply(patched);
                setOpen(false);
              }
            }}
          >
            <SelectTrigger className="h-8 w-full text-sm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {operatorsFor(spec.kind).map((op) => (
                <SelectItem key={op} value={op}>
                  {operatorLabel(op, spec.kind)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {needsValue(draft.op) && (
            <div className="flex items-center gap-2">
              <Input
                autoFocus
                type={inputType}
                value={draft.value ?? ""}
                onChange={(e) => set({ value: e.target.value })}
                onKeyDown={(e) => {
                  if (e.key === "Enter") commit();
                  if (e.key === "Escape") setOpen(false);
                }}
                placeholder={needsSecondValue(draft.op) ? "From" : "Value"}
                className="h-8 text-sm"
              />
              {needsSecondValue(draft.op) && (
                <Input
                  type={inputType}
                  value={draft.value2 ?? ""}
                  onChange={(e) => set({ value2: e.target.value })}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") commit();
                    if (e.key === "Escape") setOpen(false);
                  }}
                  placeholder="To"
                  className="h-8 text-sm"
                />
              )}
            </div>
          )}

          <div className="flex items-center justify-between gap-2 pt-0.5">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-xs"
              disabled={!active && !draft.value}
              onClick={() => {
                onChange(null);
                setOpen(false);
              }}
            >
              <X className="mr-1 h-3 w-3" />
              Clear
            </Button>
            <Button type="button" size="sm" className="h-7 px-3 text-xs" onClick={commit}>
              Apply
            </Button>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
