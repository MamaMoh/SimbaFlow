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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { StatusUpdateSheet } from "@/components/workflow/status-update-sheet";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import { DocumentUploader } from "@/components/candidates/document-uploader";
import { lmisApi, nextLmisMilestone, type LmisBoardRow } from "@/lib/api/lmis";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import { Eye, MoreHorizontal, Upload } from "lucide-react";

type Props = {
  candidate: LmisBoardRow;
  onMutate: () => void;
};

export function LmisRowActions({ candidate, onMutate }: Props) {
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("lmis.update") || hasPermission("system.admin");
  const canDoc = hasPermission("lmis.document") || hasPermission("lmis.update") || hasPermission("system.admin");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id);

  const [paidOpen, setPaidOpen] = useState(false);
  const [milestoneOpen, setMilestoneOpen] = useState(false);
  const [docOpen, setDocOpen] = useState(false);

  const insurance = candidate.insurance ?? candidate.statusValues?.insurance ?? "";
  const milestone = candidate.milestone ?? candidate.statusValues?.milestone ?? "";
  const next = nextLmisMilestone(milestone);
  const canAdvanceMilestone = insurance === "Available" && !!next;

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  return (
    <div className="flex justify-center">
      <DropdownMenu>
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
                {(insurance === "Insurance Unpaid" || !insurance) && (
                  <DropdownMenuItem onClick={() => setPaidOpen(true)}>
                    Mark insurance paid
                  </DropdownMenuItem>
                )}
                {canAdvanceMilestone && (
                  <DropdownMenuItem onClick={() => setMilestoneOpen(true)}>
                    Advance to {next}
                  </DropdownMenuItem>
                )}
              </>
            )}
            {canDoc && (
              <DropdownMenuItem onClick={() => setDocOpen(true)}>
                <Upload className="mr-2 h-3.5 w-3.5" />
                Upload LMIS document
              </DropdownMenuItem>
            )}
            {actions.length > 0 && <DropdownMenuSeparator />}
            <WorkflowActionItems candidateId={candidate.id} actions={actions} onExecuted={refresh} />
          </DropdownMenuContent>
      </DropdownMenu>

      <StatusUpdateSheet
        open={paidOpen}
        onOpenChange={setPaidOpen}
        title="Mark insurance paid"
        description="Sets Insurance Paid then Available in one audited chain."
        fields={[
          { name: "paymentDate", label: "Payment date", type: "date" },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={async (v) => {
          try {
            await lmisApi.recordInsurancePaid(
              candidate.id,
              v.paymentDate || undefined,
              v.notes
            );
            toast.success("Insurance marked paid → Available");
            refresh();
          } catch (e) {
            toast.error(e instanceof Error ? e.message : "Failed");
            throw e;
          }
        }}
      />
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
      <Dialog open={docOpen} onOpenChange={setDocOpen}>
        <DialogContent className="z-[200]">
          <DialogHeader>
            <DialogTitle>Upload LMIS document</DialogTitle>
          </DialogHeader>
          <DocumentUploader
            candidateId={candidate.id}
            defaultDocumentType={4}
            onUploaded={() => {
              toast.success("Document uploaded");
              setDocOpen(false);
              refresh();
            }}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}
