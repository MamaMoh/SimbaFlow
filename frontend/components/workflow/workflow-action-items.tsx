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
          className="items-start"
          onSelect={(e) => {
            e.preventDefault();
            void run(action);
          }}
        >
          {pendingId === action.transitionRuleId ? (
            <Loader2 className="mr-2 mt-0.5 h-4 w-4 shrink-0 animate-spin" />
          ) : (
            <ArrowRight className="mr-2 mt-0.5 h-4 w-4 shrink-0" />
          )}
          <span className="flex min-w-0 flex-col">
            <span>{action.buttonLabel}</span>
            {/* The blocker is spelled out, so a greyed-out step says what it is waiting on. */}
            {!action.isEnabled && action.disabledReason ? (
              <span className="text-xs leading-snug text-muted-foreground">
                {action.disabledReason}
              </span>
            ) : null}
          </span>
        </DropdownMenuItem>
      ))}
    </>
  );
}
