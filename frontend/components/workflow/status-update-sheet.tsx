"use client";

import { useState, type ReactNode } from "react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { statusTone, TONE_CLASSES, type StatusTone } from "@/lib/ui/status";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";

export type StatusField =
  | { name: string; label: string; type: "text" | "date" | "textarea"; required?: boolean; placeholder?: string; min?: string }
  | { name: string; label: string; type: "select"; required?: boolean; options: { value: string; label: string }[] };

type StatusUpdateSheetProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  fields: StatusField[];
  submitLabel?: string;
  onSubmit: (values: Record<string, string>) => Promise<void>;
};

export function StatusUpdateSheet({
  open,
  onOpenChange,
  title,
  description,
  fields,
  submitLabel = "Save",
  onSubmit,
}: StatusUpdateSheetProps) {
  const [values, setValues] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const setField = (name: string, value: string) =>
    setValues((prev) => ({ ...prev, [name]: value }));

  const handleOpen = (next: boolean) => {
    if (!next) {
      setValues({});
      setError(null);
    }
    onOpenChange(next);
  };

  const handleSubmit = async () => {
    for (const f of fields) {
      if (f.required && !values[f.name]?.trim()) {
        setError(`${f.label} is required`);
        return;
      }
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(values);
      handleOpen(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Request failed");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={handleOpen}>
      <SheetContent className="sm:max-w-md flex flex-col">
        <SheetHeader>
          <SheetTitle>{title}</SheetTitle>
          {description && <SheetDescription>{description}</SheetDescription>}
        </SheetHeader>
        <div className="flex-1 space-y-4 py-4 overflow-y-auto">
          {fields.map((field) => (
            <div key={field.name} className="space-y-1.5">
              <Label>
                {field.label}
                {field.required && <span className="text-red-500"> *</span>}
              </Label>
              {field.type === "select" ? (
                <Select
                  value={values[field.name] || undefined}
                  onValueChange={(v) => setField(field.name, v)}
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder={`Select ${field.label.toLowerCase()}`} />
                  </SelectTrigger>
                  <SelectContent className="z-[200]">
                    {field.options.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : field.type === "textarea" ? (
                <Textarea
                  value={values[field.name] || ""}
                  onChange={(e) => setField(field.name, e.target.value)}
                  placeholder={field.placeholder}
                  rows={3}
                />
              ) : (
                <Input
                  type={field.type}
                  min={"min" in field ? field.min : undefined}
                  value={values[field.name] || ""}
                  onChange={(e) => setField(field.name, e.target.value)}
                  placeholder={field.placeholder}
                />
              )}
            </div>
          ))}
          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>
        <SheetFooter>
          <Button type="button" variant="outline" onClick={() => handleOpen(false)}>
            Cancel
          </Button>
          <Button
            type="button"
            onClick={handleSubmit}
            disabled={submitting}
            className="bg-green-800 hover:bg-green-900 text-white"
          >
            {submitting ? "Saving…" : submitLabel}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

export function TrackChip({
  label,
  value,
  warn,
}: {
  label: string;
  value?: string | null;
  warn?: boolean;
}): ReactNode {
  // Colors come from the canonical tone system so board chips match the rest of the app.
  const tone: StatusTone = warn ? "warning" : statusTone(value);
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2 py-0.5 text-xs whitespace-nowrap",
        TONE_CLASSES[tone],
      )}
    >
      <span className="mr-1 opacity-70">{label}:</span>
      {value ? value.replace(/_/g, " ") : "—"}
    </span>
  );
}
