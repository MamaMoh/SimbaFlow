"use client";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import { arrivalApi, type ArrivalBoardRow } from "@/lib/api/arrival";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import { AlertTriangle, Eye, MoreHorizontal } from "lucide-react";
import Link from "next/link";

type Props = {
  candidate: ArrivalBoardRow;
  onMutate: () => void;
  /** Stage this board represents; scopes the workflow buttons to it. */
  stageId?: string;
};

export function ArrivalRowActions({ candidate, onMutate, stageId }: Props) {
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("arrival.update") || hasPermission("system.admin");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id, stageId);

  const arrival = candidate.statusValues?.arrival ?? "";
  const linked = candidate.commissionLinked;

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  const run = async (fn: () => Promise<void>, ok: string) => {
    try {
      await fn();
      toast.success(ok);
      refresh();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed");
    }
  };

  return (
    <div className="flex justify-center">
      <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="z-[200] w-56">
            <DropdownMenuItem asChild>
              <Link href={`/candidates/${candidate.id}`}>
                <Eye className="mr-2 h-4 w-4" />
                View details
              </Link>
            </DropdownMenuItem>
            {canUpdate && (
              <>
                <DropdownMenuSeparator />
                {(arrival === "Pending" || arrival === "") && (
                  <DropdownMenuItem
                    onClick={() => run(() => arrivalApi.confirmArrived(candidate.id), "Arrived")}
                  >
                    Confirm arrived
                  </DropdownMenuItem>
                )}
                {arrival === "Arrived" && !linked && !candidate.hasOpenException && (
                  <DropdownMenuItem
                    onClick={() =>
                      run(() => arrivalApi.addToCommission(candidate.id), "Added to Commission")
                    }
                  >
                    Add to Commission
                  </DropdownMenuItem>
                )}
                {arrival !== "Returned" && arrival !== "Runaway" && (
                  <>
                    <DropdownMenuItem
                      onClick={() =>
                        run(
                          () => arrivalApi.flagException(candidate.id, "Returned"),
                          "Flagged Returned"
                        )
                      }
                    >
                      Flag Returned
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() =>
                        run(
                          () => arrivalApi.flagException(candidate.id, "Runaway"),
                          "Flagged Runaway"
                        )
                      }
                    >
                      Flag Runaway
                    </DropdownMenuItem>
                  </>
                )}
              </>
            )}
            {candidate.hasOpenException && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link href="/workflow/exceptions">
                    <AlertTriangle className="mr-2 h-4 w-4 text-amber-600" />
                    Open exception case
                  </Link>
                </DropdownMenuItem>
              </>
            )}
            {actions.length > 0 && <DropdownMenuSeparator />}
            <WorkflowActionItems candidateId={candidate.id} actions={actions} onExecuted={refresh} />
          </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
