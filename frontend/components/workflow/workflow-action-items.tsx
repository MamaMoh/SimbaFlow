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
 *
 * Only steps that can actually be taken are listed. A blocked step used to be shown greyed out
 * with the blocker spelled out underneath, which turned a short menu of things you can do into a
 * long one mostly describing things you cannot — the board chips already say where the candidate
 * is up to.
 */
/** True when any transition can actually be taken — use it to decide on a separator. */
export function hasEnabledActions(actions: AvailableAction[] | undefined): boolean {
  return (actions ?? []).some((a) => a.isEnabled);
}

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

  const available = (actions ?? []).filter((a) => a.isEnabled);
  if (available.length === 0) return null;

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
      {available.map((action) => (
        <DropdownMenuItem
          key={action.transitionRuleId}
          disabled={pendingId === action.transitionRuleId}
          onSelect={(e) => {
            e.preventDefault();
            void run(action);
          }}
        >
          {pendingId === action.transitionRuleId ? (
            <Loader2 className="mr-2 h-4 w-4 shrink-0 animate-spin" />
          ) : (
            <ArrowRight className="mr-2 h-4 w-4 shrink-0" />
          )}
          {action.buttonLabel}
        </DropdownMenuItem>
      ))}
    </>
  );
}
