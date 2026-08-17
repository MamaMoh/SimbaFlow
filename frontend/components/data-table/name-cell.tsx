"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";

/**
 * Standard name cell for every table. Long candidate names would otherwise
 * stretch the column and push the rest of the table off screen, so the text is
 * clamped to one line with an ellipsis; the full value stays available as a
 * tooltip (and on the detail page).
 */
export function NameCell({
  href,
  name,
  subtitle,
  className,
}: {
  href?: string;
  name?: string | null;
  subtitle?: string | null;
  className?: string;
}) {
  const label = name?.trim() || "—";
  return (
    <div className={cn("max-w-[220px]", className)}>
      {href ? (
        <Link
          href={href}
          title={label}
          className="block truncate font-medium text-foreground hover:underline"
        >
          {label}
        </Link>
      ) : (
        <span title={label} className="block truncate font-medium">
          {label}
        </span>
      )}
      {subtitle ? (
        <span title={subtitle} className="block truncate text-xs text-muted-foreground">
          {subtitle}
        </span>
      ) : null}
    </div>
  );
}
