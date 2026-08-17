"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import type { AvailableAction } from "@/types/workflow";
import { executeTransition } from "@/lib/api/workflow";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";

type ActionButtonBarProps = {
  candidateId: string;
  actions: AvailableAction[];
  onExecuted?: () => void;
  className?: string;
};

export function ActionButtonBar({
  candidateId,
  actions,
  onExecuted,
  className,
}: ActionButtonBarProps) {
  const [pendingId, setPendingId] = useState<string | null>(null);

  const handleClick = async (action: AvailableAction) => {
    if (!action.isEnabled || pendingId) return;

    const confirmed = window.confirm(`Execute “${action.buttonLabel}”?`);
    if (!confirmed) return;

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

  if (actions.length === 0) {
    return (
      <span className="text-xs text-muted-foreground">No actions available</span>
    );
  }

  return (
    <TooltipProvider>
      <div className={`flex flex-wrap gap-1.5 ${className ?? ""}`}>
        {actions.map((action) => {
          const button = (
            <Button
              key={action.transitionRuleId}
              size="sm"
              variant={action.isEnabled ? "default" : "outline"}
              disabled={!action.isEnabled || pendingId === action.transitionRuleId}
              onClick={() => handleClick(action)}
              data-testid={`action-button-${action.transitionRuleId}`}
              className={
                action.isEnabled
                  ? "h-9 px-4 text-sm"
                  : "h-9 px-4 text-sm text-muted-foreground"
              }
            >
              {pendingId === action.transitionRuleId && (
                <Loader2 className="mr-1 h-3 w-3 animate-spin" />
              )}
              {action.buttonLabel}
            </Button>
          );

          if (!action.isEnabled && action.disabledReason) {
            return (
              <Tooltip key={action.transitionRuleId}>
                <TooltipTrigger asChild>
                  <span tabIndex={0}>{button}</span>
                </TooltipTrigger>
                <TooltipContent>
                  <p>{action.disabledReason}</p>
                </TooltipContent>
              </Tooltip>
            );
          }

          return button;
        })}
      </div>
    </TooltipProvider>
  );
}
