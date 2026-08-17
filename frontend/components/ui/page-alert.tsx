"use client";

import { AlertCircle, CheckCircle2, Info } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { cn } from "@/lib/utils";

type PageAlertVariant = "info" | "success" | "error";

const icons = {
  info: Info,
  success: CheckCircle2,
  error: AlertCircle,
};

type PageAlertProps = {
  variant?: PageAlertVariant;
  title: string;
  description?: string;
  className?: string;
};

/** Page-level banner (use with sonner toasts for transient actions). */
export function PageAlert({
  variant = "info",
  title,
  description,
  className,
}: PageAlertProps) {
  const Icon = icons[variant];
  return (
    <Alert
      variant={variant === "error" ? "destructive" : "default"}
      className={cn(className)}
    >
      <Icon />
      <AlertTitle>{title}</AlertTitle>
      {description ? <AlertDescription>{description}</AlertDescription> : null}
    </Alert>
  );
}

type AccessDeniedProps = {
  resource?: string;
};

export function AccessDenied({ resource }: AccessDeniedProps) {
  return (
    <div className="p-6">
      <PageAlert
        variant="error"
        title="Access denied"
        description={
          resource
            ? `You do not have permission to view ${resource}.`
            : "You do not have permission to view this page."
        }
      />
    </div>
  );
}

type LoadErrorProps = {
  message?: string;
  onRetry?: () => void;
};

export function LoadError({ message, onRetry }: LoadErrorProps) {
  return (
    <div className="space-y-2">
      <PageAlert
        variant="error"
        title="Failed to load data"
        description={
          message ||
          (onRetry
            ? "Something went wrong. Try again or refresh the page."
            : "Something went wrong. Refresh the page and try again.")
        }
      />
      {onRetry ? (
        <button
          type="button"
          onClick={onRetry}
          className="text-sm font-medium text-primary underline-offset-4 hover:underline"
        >
          Retry
        </button>
      ) : null}
    </div>
  );
}
