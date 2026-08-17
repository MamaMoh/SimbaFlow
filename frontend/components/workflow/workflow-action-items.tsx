"use client";

import { useState } from "react";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { ArrowRight, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { executeTransition } from "@/lib/api/workflow";
import type { AvailableAction } from "@/types/workflow";

/**
 * Renders the candidate's available workflow transitions as items inside a row's
 * ⋯ menu, so every board exposes actions the same way instead of stacking loose
 * buttons in the Actions column.
 */
export function WorkflowActionItems({
  candidateId,
  actions,
  onExecuted,
}: {
  candidateId: string;
  actions: AvailableAction[];
  onExecuted?: () => void;
}) {
  const [pendingId, setPendingId] = useState<string | null>(null);

  if (!actions || actions.length === 0) return null;

  const run = async (action: AvailableAction) => {
    if (!action.isEnabled || pendingId) return;
    if (!window.confirm(`Execute “${action.buttonLabel}”?`)) return;
    setPendingId(action.transitionRuleId);
    try {
      await executeTransition(candidateId, action.transitionRuleId);
      toast.success(`Executed: ${action.buttonLabel}`);
      onExecuted?.();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Transition failed");
    } finally {
      setPendingId(null);
    }
  };

  return (
    <>
      {actions.map((action) => (
        <DropdownMenuItem
          key={action.transitionRuleId}
          disabled={!action.isEnabled || pendingId === action.transitionRuleId}
          title={!action.isEnabled ? action.disabledReason ?? undefined : undefined}
          onSelect={(e) => {
            e.preventDefault();
            void run(action);
          }}
        >
          {pendingId === action.transitionRuleId ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <ArrowRight className="mr-2 h-4 w-4" />
          )}
          {action.buttonLabel}
          {!action.isEnabled && action.disabledReason ? (
            <span className="ml-2 text-xs text-muted-foreground">
              ({action.disabledReason})
            </span>
          ) : null}
        </DropdownMenuItem>
      ))}
    </>
  );
}
