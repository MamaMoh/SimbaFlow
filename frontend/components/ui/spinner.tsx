// components/ui/spinner.tsx
import * as React from "react";
import { cn } from "@/lib/utils";

type SpinnerProps = {
  size?: "sm" | "md" | "lg" | number; // number allows custom px size
  variant?: "primary" | "secondary" | "muted" | string; // string allows custom color class
  className?: string;
  style?: React.CSSProperties; // custom inline styles
  label?: string; // accessible label
  borderWidth?: number; // optional custom border width
};

export function Spinner({
  size = "md",
  variant = "primary",
  className,
  style,
  label = "Loading...",
  borderWidth,
}: SpinnerProps) {
  const sizeClasses: Record<string, string> = {
    sm: "h-4 w-4",
    md: "h-6 w-6",
    lg: "h-10 w-10",
  };

  const borderClasses: Record<string, string> = {
    primary: "border-primary border-t-transparent",
    secondary: "border-secondary border-t-transparent",
    muted: "border-muted-foreground border-t-transparent",
  };

  const finalSizeClass =
    typeof size === "number"
      ? "" // we’ll use inline styles for custom pixel size
      : sizeClasses[size];

  const finalBorderClass = borderClasses[variant] || variant; // allows passing custom Tailwind color class

  return (
    <div
      role="status"
      aria-label={label}
      aria-busy="true"
      className={cn(
        "animate-spin rounded-full border-solid",
        finalSizeClass,
        finalBorderClass,
        className
      )}
      style={{
        width: typeof size === "number" ? size : undefined,
        height: typeof size === "number" ? size : undefined,
        borderWidth: borderWidth ?? (size === "lg" ? 4 : 2),
        ...style,
      }}
    />
  );
}
