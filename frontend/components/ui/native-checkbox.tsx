"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type">;

export function NativeCheckbox({ className, ...props }: Props) {
  return (
    <input
      type="checkbox"
      className={cn(
        "size-4 shrink-0 rounded-[4px] border border-input bg-background shadow-xs",
        "checked:bg-primary checked:border-primary accent-primary",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/50",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}
