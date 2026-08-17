"use client";

import { StatusBadge } from "@/components/ui/status-badge";
import { commissionTone } from "@/lib/ui/status";

/** Commission status chip — delegates to the canonical StatusBadge. */
export function CommissionStatusBadge({
  status,
  className,
}: {
  status: string;
  className?: string;
}) {
  return (
    <StatusBadge tone={commissionTone(status)} value={status || "—"} className={className} />
  );
}
