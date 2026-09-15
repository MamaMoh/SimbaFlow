"use client";

import { useState, type ReactNode } from "react";
import { ChevronDown, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * A collapsible titled section.
 *
 * Shared between the candidate form and the candidate detail page so the record reads the same
 * whether you are filling it in or looking at it — the same headings, in the same order, that
 * collapse the same way. A candidate has roughly fifty fields, and being able to fold the parts
 * you are not working on is what keeps either page usable.
 */
export function FormSection({
  icon: Icon,
  title,
  description,
  /** Shown on the right of the header — e.g. how many fields are still blank. */
  meta,
  defaultOpen = true,
  children,
  className,
}: {
  icon: LucideIcon;
  title: string;
  description?: string;
  meta?: ReactNode;
  defaultOpen?: boolean;
  children: ReactNode;
  className?: string;
}) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <section
      className={cn(
        "rounded-xl border border-slate-200/90 bg-white shadow-sm",
        className
      )}
    >
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center gap-2.5 border-b border-slate-100 px-4 py-3 text-left transition-colors hover:bg-slate-50/70"
      >
        <Icon className="h-4 w-4 shrink-0 text-emerald-700" />
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-semibold tracking-tight text-slate-900">{title}</h3>
          {description ? (
            <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">{description}</p>
          ) : null}
        </div>
        {meta ? <span className="shrink-0 text-xs text-muted-foreground">{meta}</span> : null}
        <ChevronDown
          className={cn(
            "h-4 w-4 shrink-0 text-slate-400 transition-transform",
            !open && "-rotate-90"
          )}
        />
      </button>
      {open ? <div className="space-y-4 px-4 py-4">{children}</div> : null}
    </section>
  );
}
