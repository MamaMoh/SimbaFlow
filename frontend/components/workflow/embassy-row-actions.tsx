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
import { embassyApi, type EmbassyBoardRow } from "@/lib/api/embassy";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import { Eye, MoreHorizontal } from "lucide-react";
import Link from "next/link";

type Mode =
  | "book-medical"
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
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id, stageId);

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
      <DropdownMenu>
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
          <DropdownMenuItem asChild>
            <Link href={`/candidates/${candidate.id}`}>
              <Eye className="mr-2 h-4 w-4" />
              View details
            </Link>
          </DropdownMenuItem>

          {isCaseExec ? (
            canCaseSubmit && visa === "Ready" ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setMode("visa-submit")}>
                  Submit documentation
                </DropdownMenuItem>
              </>
            ) : null
          ) : (
            <>
              {canUpdate && (
                <>
                  <DropdownMenuSeparator />
                  {(medical === "Pending" || medical === "" || medical === "Expired") && (
                    <DropdownMenuItem onClick={() => setMode("book-medical")}>
                      Book medical
                    </DropdownMenuItem>
                  )}
                  {medical === "Booked" && (
                    <DropdownMenuItem onClick={() => setMode("medical-result")}>
                      Record medical result
                    </DropdownMenuItem>
                  )}
                  {(tasheer === "Pending" || tasheer === "" || tasheer === "Expired") && (
                    <DropdownMenuItem onClick={() => setMode("book-tasheer")}>
                      Book tasheer
                    </DropdownMenuItem>
                  )}
                  {tasheer === "Booked" && (
                    <DropdownMenuItem onClick={() => setMode("tasheer-result")}>
                      Record tasheer result
                    </DropdownMenuItem>
                  )}
                  {medical === "Fit" && tasheer === "Book Done" && !visa && (
                    <DropdownMenuItem onClick={() => setMode("visa-ready")}>
                      Set visa Ready
                    </DropdownMenuItem>
                  )}
                  {canOutcome && visa === "Submitted" && (
                    <DropdownMenuItem onClick={() => setMode("visa-outcome")}>
                      Visa outcome
                    </DropdownMenuItem>
                  )}
                  {canOutcome && visa === "Rejected" && (
                    <DropdownMenuItem onClick={() => setMode("visa-resubmit")}>
                      Resubmit visa
                    </DropdownMenuItem>
                  )}
                </>
              )}
              {actions.length > 0 && <DropdownMenuSeparator />}
              <WorkflowActionItems
                candidateId={candidate.id}
                actions={actions}
                onExecuted={refresh}
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
        open={mode === "book-medical"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Book medical"
        fields={[
          { name: "appointmentDate", label: "Appointment date", type: "date", required: true },
          { name: "facilityName", label: "Facility", type: "text", required: true },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        onSubmit={(v) =>
          run(
            () =>
              embassyApi.bookMedical(
                candidateId,
                v.appointmentDate,
                v.facilityName,
                v.notes
              ),
            "Medical booked"
          )
        }
      />
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
          { name: "referenceNumber", label: "Reference number", type: "text" },
          { name: "notes", label: "Notes", type: "textarea" },
        ]}
        submitLabel="Submit"
        onSubmit={(v) =>
          run(
            () =>
              embassyApi.submitVisa(
                candidateId,
                v.submissionDate || undefined,
                v.referenceNumber || undefined,
                v.notes
              ),
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
