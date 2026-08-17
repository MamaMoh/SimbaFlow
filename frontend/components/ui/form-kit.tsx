"use client";

import { useState } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Label } from "@/components/ui/label";

/**
 * Standard form building blocks. Goal: simple, forgiving forms for
 * non-technical users — clear labels, obvious required markers, plain-language
 * help, generous spacing, and consistent section/action layout everywhere.
 */

export function FormSection({
  title,
  description,
  children,
  collapsible = false,
  defaultOpen = true,
  className,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
  collapsible?: boolean;
  defaultOpen?: boolean;
  className?: string;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const body = <div className="grid gap-4 sm:grid-cols-2">{children}</div>;

  return (
    <section className={cn("rounded-xl border bg-card p-5 shadow-sm", className)}>
      {collapsible ? (
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="flex w-full items-center justify-between gap-2 text-left"
          aria-expanded={open}
        >
          <div>
            <h2 className="text-base font-semibold">{title}</h2>
            {description && (
              <p className="text-sm text-muted-foreground">{description}</p>
            )}
          </div>
          <ChevronDown
            className={cn("h-5 w-5 shrink-0 text-muted-foreground transition-transform", open && "rotate-180")}
          />
        </button>
      ) : (
        <div className="mb-4">
          <h2 className="text-base font-semibold">{title}</h2>
          {description && (
            <p className="text-sm text-muted-foreground">{description}</p>
          )}
        </div>
      )}
      {(!collapsible || open) && <div className={cn(collapsible && "mt-4")}>{body}</div>}
    </section>
  );
}

/** A labelled field. `full` spans both columns of a FormSection grid. */
export function Field({
  label,
  htmlFor,
  required,
  help,
  error,
  full,
  children,
  className,
}: {
  label: string;
  htmlFor?: string;
  required?: boolean;
  help?: string;
  error?: string;
  full?: boolean;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("space-y-1.5", full && "sm:col-span-2", className)}>
      <Label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
        {required && <span className="ml-0.5 text-destructive" aria-hidden>*</span>}
      </Label>
      {children}
      {help && !error && <p className="text-xs text-muted-foreground">{help}</p>}
      {error && <p className="text-xs font-medium text-destructive">{error}</p>}
    </div>
  );
}

/** Right-aligned action bar. Convention: Cancel (ghost) then primary Save. */
export function FormActions({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex items-center justify-end gap-2 border-t pt-4",
        className,
      )}
    >
      {children}
    </div>
  );
}
