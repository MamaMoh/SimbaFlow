"use client";

import { useState } from "react";
import {
  DropdownMenuItem,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { ArrowRight, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { executeTransition } from "@/lib/api/workflow";
import type { AvailableAction } from "@/types/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";

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

export function WorkflowActionItems({
  candidateId,
  actions,
  onExecuted,
  separatorBefore,
}: {
  candidateId: string;
  actions: AvailableAction[];
  onExecuted?: () => void;
  /**
   * Draw a divider above the steps when there are any. The callers used to decide this for
   * themselves from the raw action list, which left a divider hanging under the last item once the
   * permission check emptied the list here.
   */
  separatorBefore?: boolean;
}) {
  const { hasPermission } = usePermissions();
  const [pendingId, setPendingId] = useState<string | null>(null);

  // The API now withholds these from anyone without workflow.execute, so this list should already
  // be empty for them. Checking here too means a cached response from before that gate cannot put
  // a step in front of someone who will only get a 403 for pressing it.
  const available = hasPermission("workflow.execute")
    ? (actions ?? []).filter((a) => a.isEnabled)
    : [];
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
      {separatorBefore && <DropdownMenuSeparator />}
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
