"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StatusUpdateSheet } from "@/components/workflow/status-update-sheet";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import { CandidateDocumentItems } from "@/components/workflow/candidate-document-items";
import { MoveBackStageItem } from "@/components/workflow/move-back-stage-item";
import { CopyToFormItem } from "@/components/workflow/copy-to-form-item";
import { lmisApi, nextLmisMilestone, type LmisBoardRow } from "@/lib/api/lmis";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import {
  ArrowRight,
  BadgeCheck,
  CircleSlash,
  Eye,
  MoreHorizontal,
  Pencil,
} from "lucide-react";

type Props = {
  candidate: LmisBoardRow;
  onMutate: () => void;
  /** Stage this board represents; scopes the workflow buttons to it. */
  stageId?: string;
};

export function LmisRowActions({ candidate, onMutate, stageId }: Props) {
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("lmis.update") || hasPermission("system.admin");
  // The candidate's own page, and the paperwork below it, are read under candidate.read. Reaching
  // this board is a different permission, and holding one does not imply the other, so the link has
  // to ask for what the page it points at requires.
  const canRead = hasPermission("candidate.read");
  const canEditCandidate = hasPermission("candidate.update");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id, stageId);

  const [milestoneOpen, setMilestoneOpen] = useState(false);

  const insurance = candidate.insurance ?? candidate.statusValues?.insurance ?? "";
  const milestone = candidate.milestone ?? candidate.statusValues?.milestone ?? "";
  const next = nextLmisMilestone(milestone);
  const canAdvanceMilestone = insurance === "Available" && !!next;

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  const run = async (fn: () => Promise<unknown>, ok: string) => {
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
            {/* View details and Edit first, together, then a rule — the same opening group
                every row menu in the app has, so the two items people reach for without
                reading are always in the same two places. */}
            {canRead && (
              <DropdownMenuItem asChild>
                <Link href={`/candidates/${candidate.id}`}>
                  <Eye className="mr-2 h-4 w-4" />
                  View details
                </Link>
              </DropdownMenuItem>
            )}
            {canEditCandidate && (
              <DropdownMenuItem asChild>
                <Link href={`/candidates/${candidate.id}/edit`}>
                  <Pencil className="mr-2 h-4 w-4" />
                  Edit
                </Link>
              </DropdownMenuItem>
            )}
            {canRead || canEditCandidate ? <DropdownMenuSeparator /> : null}
            {/* LMIS needs the visa form and the LMIS contract fill; the CV, the contract and the
                insurance copy belong to the stages before it. */}
            <CandidateDocumentItems candidateId={candidate.id} only={["visa"]} />
            <CopyToFormItem form="lmis" candidateId={candidate.id} candidateName={candidate.fullName} />
            <DropdownMenuSeparator />
            <MoveBackStageItem
              candidateId={candidate.id}
              candidateName={candidate.fullName}
              stageId={stageId}
              isMirror={candidate.isMirror}
              onDone={refresh}
            />
            {canUpdate && (
              <>
                <DropdownMenuSeparator />
                {/* Paid and unpaid are the same kind of answer, so they are the same kind of
                    click. This one used to open a form asking for a payment date and a note
                    that nobody filled in — two more steps to say a thing that has two
                    possible values. */}
                {(insurance === "Insurance Unpaid" || !insurance) && (
                  <DropdownMenuItem
                    onSelect={(e) => {
                      e.preventDefault();
                      void run(
                        () => lmisApi.recordInsurancePaid(candidate.id),
                        "Insurance marked paid → Available",
                      );
                    }}
                  >
                    <BadgeCheck className="mr-2 h-4 w-4" />
                    Mark insurance paid
                  </DropdownMenuItem>
                )}
                {insurance && insurance !== "Insurance Unpaid" && (
                  <DropdownMenuItem
                    onSelect={(e) => {
                      e.preventDefault();
                      void run(
                        () => lmisApi.recordInsuranceUnpaid(candidate.id),
                        "Insurance set back to unpaid",
                      );
                    }}
                  >
                    <CircleSlash className="mr-2 h-4 w-4" />
                    Mark insurance unpaid
                  </DropdownMenuItem>
                )}
                {/* The milestone's own name, not "Advance to" it. The menu is a list of
                    things to do; the verb was the same on every one of them and the only
                    part that differed was the word at the end. */}
                {canAdvanceMilestone && (
                  <DropdownMenuItem onClick={() => setMilestoneOpen(true)}>
                    <ArrowRight className="mr-2 h-4 w-4" />
                    {next}
                  </DropdownMenuItem>
                )}
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

      <StatusUpdateSheet
        open={milestoneOpen}
        onOpenChange={setMilestoneOpen}
        title="Advance milestone"
        description={next ? `Next step: ${next}` : "No further milestone"}
        fields={[{ name: "notes", label: "Notes", type: "textarea" }]}
        submitLabel={next ? `Advance to ${next}` : "Advance"}
        onSubmit={async (v) => {
          if (!next) throw new Error("No next milestone");
          try {
            await lmisApi.advanceMilestone(candidate.id, next, v.notes);
            toast.success(`Milestone: ${next}`);
            refresh();
          } catch (e) {
            toast.error(e instanceof Error ? e.message : "Failed");
            throw e;
          }
        }}
      />
    </div>
  );
}
