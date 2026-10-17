"use client";

import { useState } from "react";
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
import { embassyApi, type EmbassyBoardRow } from "@/lib/api/embassy";
import { CONTRACT_PARTY_FIELDS } from "@/lib/api/candidates";
import { useVisaDetails } from "@/components/workflow/visa-details-host";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import {
  BadgeCheck,
  CircleSlash,
  CalendarClock,
  ClipboardCheck,
  ClipboardPaste,
  Eye,
  MoreHorizontal,
  Pencil,
  RotateCcw,
  Send,
  Stamp,
  Stethoscope,
} from "lucide-react";
import Link from "next/link";
import {
  bookMedicalBlock,
  bookTasheerBlock,
  recordMedicalResultBlock,
  recordTasheerResultBlock,
  unbookMedicalBlock,
} from "@/lib/workflow/board-action-rules";

type Mode =
  | "medical-result"
  | "book-tasheer"
  | "tasheer-result"
  | "visa-ready"
  | "visa-submit"
  | "visa-outcome"
  | "visa-resubmit"
  | null;

type Props = {
  candidate: EmbassyBoardRow;
  onMutate: () => void;
  /** case-executive board: only submit */
  variant?: "embassy" | "case-executive";
  /** Stage this board represents; scopes the workflow buttons to it. */
  stageId?: string;
};

export function EmbassyRowActions({ candidate, onMutate, stageId, variant = "embassy" }: Props) {
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("embassy.update") || hasPermission("system.admin");
  const canCaseSubmit =
    hasPermission("embassy.case_submit") || hasPermission("system.admin");
  const canOutcome =
    hasPermission("embassy.visa_outcome") || hasPermission("system.admin");
  // The candidate's own page, and the paperwork below it, are read under candidate.read. Reaching
  // this board is a different permission, and holding one does not imply the other, so the link has
  // to ask for what the page it points at requires.
  const canRead = hasPermission("candidate.read");
  const canEditCandidate = hasPermission("candidate.update");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id, stageId);
  const askForVisaDetails = useVisaDetails();

  const [mode, setMode] = useState<Mode>(null);
  const medical = candidate.statusValues?.medical ?? "";
  const tasheer = candidate.statusValues?.tasheer ?? "";
  const visa = candidate.statusValues?.visa ?? "";

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
      throw e;
    }
  };

  const isCaseExec = variant === "case-executive";

  return (
    <div className="flex justify-center">
      <DropdownMenu modal={false}>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-8 w-8 p-0"
            aria-label="Row actions"
          >
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
          <CandidateDocumentItems
            candidateId={candidate.id}
            visaFormMissing={candidate.visaFormMissing}
          />
          {/*
            The employer block off the portal, into the record, without opening the candidate.

            This is the one piece of typing the embassy desk does out of another tab: the
            employer's name, the address the candidate is going to, the visa number and a
            mobile, printed together under "Contract parties" and retyped one box at a time.
            The dialog this opens takes the whole block pasted at once.

            Offered whatever is already on file, rather than only when something is missing. A
            sponsor's number that changed is the ordinary case, and a list of gaps would never
            have offered it.
          */}
          {canEditCandidate && (
            <DropdownMenuItem
              onSelect={(e) => {
                e.preventDefault();
                askForVisaDetails({
                  gaps: [
                    {
                      id: candidate.id,
                      fullName: candidate.fullName,
                      fields: CONTRACT_PARTY_FIELDS,
                    },
                  ],
                  requireAll: false,
                  onFilled: refresh,
                  onCancelled: refresh,
                });
              }}
            >
              <ClipboardPaste className="mr-2 h-4 w-4" />
              Contract parties (paste)
            </DropdownMenuItem>
          )}
          <DropdownMenuSeparator />
          <MoveBackStageItem
            candidateId={candidate.id}
            candidateName={candidate.fullName}
            stageId={stageId}
            isMirror={candidate.isMirror}
            onDone={refresh}
          />

          {isCaseExec ? (
            canCaseSubmit && visa === "Ready" ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setMode("visa-submit")}>
                  <Send className="mr-2 h-4 w-4" />
                  Submit documentation
                </DropdownMenuItem>
              </>
            ) : null
          ) : (
            <>
              {canUpdate && (
                <>
                  <DropdownMenuSeparator />
                  {bookMedicalBlock(medical) === null && (
                    <DropdownMenuItem
                      onSelect={(e) => {
                        e.preventDefault();
                        void run(() => embassyApi.bookMedical(candidate.id), "Medical booked");
                      }}
                    >
                      <Stethoscope className="mr-2 h-4 w-4" />
                      Book medical
                    </DropdownMenuItem>
                  )}
                  {unbookMedicalBlock(medical) === null && (
                    <DropdownMenuItem
                      onSelect={(e) => {
                        e.preventDefault();
                        void run(() => embassyApi.unbookMedical(candidate.id), "Medical unbooked");
                      }}
                    >
                      <CircleSlash className="mr-2 h-4 w-4" />
                      Mark unbooked
                    </DropdownMenuItem>
                  )}
                  {recordMedicalResultBlock(medical) === null && (
                    <DropdownMenuItem onClick={() => setMode("medical-result")}>
                      <ClipboardCheck className="mr-2 h-4 w-4" />
                      Record medical result
                    </DropdownMenuItem>
                  )}
                  {bookTasheerBlock(tasheer) === null && (
                    <DropdownMenuItem onClick={() => setMode("book-tasheer")}>
                      <CalendarClock className="mr-2 h-4 w-4" />
                      Book tasheer
                    </DropdownMenuItem>
                  )}
                  {recordTasheerResultBlock(tasheer) === null && (
                    <DropdownMenuItem onClick={() => setMode("tasheer-result")}>
                      <ClipboardCheck className="mr-2 h-4 w-4" />
                      Record tasheer result
                    </DropdownMenuItem>
                  )}
                  {medical === "Fit" && tasheer === "Book Done" && !visa && (
                    <DropdownMenuItem onClick={() => setMode("visa-ready")}>
                      <BadgeCheck className="mr-2 h-4 w-4" />
                      Set visa Ready
                    </DropdownMenuItem>
                  )}
                  {/*
                    Submission also belongs here, not only on the Case Executive board. Without it
                    a candidate at visa Ready has no action on this board at all, which reads as
                    the pipeline being broken rather than as work sitting with another desk.
                  */}
                  {canCaseSubmit && visa === "Ready" && (
                    <DropdownMenuItem onClick={() => setMode("visa-submit")}>
                      <Send className="mr-2 h-4 w-4" />
                      Submit documentation
                    </DropdownMenuItem>
                  )}
                  {canOutcome && visa === "Submitted" && (
                    <DropdownMenuItem onClick={() => setMode("visa-outcome")}>
                      <Stamp className="mr-2 h-4 w-4" />
                      Visa outcome
                    </DropdownMenuItem>
                  )}
                  {canOutcome && visa === "Rejected" && (
                    <DropdownMenuItem onClick={() => setMode("visa-resubmit")}>
                      <RotateCcw className="mr-2 h-4 w-4" />
                      Resubmit visa
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
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
      <StatusSheets candidateId={candidate.id} mode={mode} setMode={setMode} run={run} />
    </div>
  );
}

function StatusSheets({
  candidateId,
  mode,
  setMode,
  run,
}: {
  candidateId: string;
  mode: Mode;
  setMode: (m: Mode) => void;
  run: (fn: () => Promise<void>, ok: string) => Promise<void>;
}) {
  return (
    <>
      <StatusUpdateSheet
        open={mode === "medical-result"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Medical result"
        fields={[
          {
            name: "result",
            label: "Result",
            type: "select",
            required: true,
            options: [
              { value: "Fit", label: "Fit" },
              { value: "Unfit", label: "Unfit" },
            ],
          },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={(v) =>
          run(
            () =>
              embassyApi.recordMedicalResult(
                candidateId,
                v.result as "Fit" | "Unfit",
                v.notes
              ),
            "Medical result saved"
          )
        }
      />
      <StatusUpdateSheet
        open={mode === "book-tasheer"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Book tasheer"
        fields={[
          { name: "appointmentDate", label: "Appointment date", type: "date", required: true },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={(v) =>
          run(
            () => embassyApi.bookTasheer(candidateId, v.appointmentDate, v.notes),
            "Tasheer booked"
          )
        }
      />
      <StatusUpdateSheet
        open={mode === "tasheer-result"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Tasheer result"
        fields={[
          {
            name: "result",
            label: "Result",
            type: "select",
            required: true,
            options: [
              { value: "Book Done", label: "Book Done" },
              { value: "Expired", label: "Expired" },
            ],
          },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={(v) =>
          run(
            () =>
              embassyApi.recordTasheerResult(
                candidateId,
                v.result as "Book Done" | "Expired",
                v.notes
              ),
            "Tasheer result saved"
          )
        }
      />
      <StatusUpdateSheet
        open={mode === "visa-ready"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Set visa Ready"
        description="Activates Case Executive mirror when clearances are complete."
        fields={[{ name: "notes", label: "Notes", type: "textarea" }]}
        submitLabel="Set Ready"
        onSubmit={(v) =>
          run(() => embassyApi.setVisaReady(candidateId, v.notes), "Visa set to Ready")
        }
      />
      <StatusUpdateSheet
        open={mode === "visa-submit"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Submit visa documentation"
        fields={[
          { name: "submissionDate", label: "Submission date", type: "date" },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        submitLabel="Submit"
        onSubmit={(v) =>
          run(
            () =>
              embassyApi.submitVisa(candidateId, v.submissionDate || undefined, v.notes),
            "Documentation submitted"
          )
        }
      />
      <StatusUpdateSheet
        open={mode === "visa-outcome"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Visa outcome"
        fields={[
          {
            name: "outcome",
            label: "Outcome",
            type: "select",
            required: true,
            options: [
              { value: "Issued", label: "Issued" },
              { value: "Rejected", label: "Rejected" },
            ],
          },
          {
            name: "rejectionReason",
            label: "Rejection reason",
            type: "textarea",
            placeholder: "Required when Rejected",
          },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={async (v) => {
          if (v.outcome === "Rejected" && !v.rejectionReason?.trim()) {
            throw new Error("Rejection reason is required");
          }
          await run(
            () =>
              embassyApi.recordVisaOutcome(
                candidateId,
                v.outcome as "Issued" | "Rejected",
                v.rejectionReason,
                v.notes
              ),
            "Visa outcome recorded"
          );
        }}
      />
      <StatusUpdateSheet
        open={mode === "visa-resubmit"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Resubmit visa"
        fields={[{ name: "notes", label: "Notes", type: "textarea" }]}
        submitLabel="Resubmit"
        onSubmit={(v) =>
          run(() => embassyApi.resubmitVisa(candidateId, v.notes), "Visa resubmitted to Ready")
        }
      />
    </>
  );
}
