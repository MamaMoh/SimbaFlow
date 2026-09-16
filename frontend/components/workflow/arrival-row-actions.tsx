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
import { CandidateDocumentItems } from "@/components/workflow/candidate-document-items";
import { arrivalApi, type ArrivalBoardRow } from "@/lib/api/arrival";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import {
  AlertTriangle,
  Coins,
  Eye,
  MoreHorizontal,
  PlaneLanding,
  Undo2,
} from "lucide-react";
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
  // The candidate's own page, and the paperwork below it, are read under candidate.read. Reaching
  // this board is a different permission, and holding one does not imply the other, so the link has
  // to ask for what the page it points at requires.
  const canRead = hasPermission("candidate.read");
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
            {canRead && (
              <>
                <DropdownMenuItem asChild>
                  <Link href={`/candidates/${candidate.id}`}>
                    <Eye className="mr-2 h-4 w-4" />
                    View details
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
              </>
            )}
            <CandidateDocumentItems candidateId={candidate.id} />
            {canUpdate && (
              <>
                <DropdownMenuSeparator />
                {(arrival === "Pending" || arrival === "") && (
                  <DropdownMenuItem
                    onClick={() => run(() => arrivalApi.confirmArrived(candidate.id), "Arrived")}
                  >
                    <PlaneLanding className="mr-2 h-4 w-4" />
                    Confirm arrived
                  </DropdownMenuItem>
                )}
                {arrival === "Arrived" && !linked && !candidate.hasOpenException && (
                  <DropdownMenuItem
                    onClick={() =>
                      run(() => arrivalApi.addToCommission(candidate.id), "Added to Commission")
                    }
                  >
                    <Coins className="mr-2 h-4 w-4" />
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
                      <Undo2 className="mr-2 h-4 w-4" />
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
                      <AlertTriangle className="mr-2 h-4 w-4" />
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
            <WorkflowActionItems
              candidateId={candidate.id}
              actions={actions}
              onExecuted={refresh}
              separatorBefore
            />
          </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
