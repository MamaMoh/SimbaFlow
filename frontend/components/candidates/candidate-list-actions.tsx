"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  MoreHorizontal,
  Eye,
  Pencil,
  Trash2,
  FileText,
  Loader2,
  ArrowRight,
} from "lucide-react";
import { toast } from "sonner";
import {
  executeTransition,
  useAvailableActions,
} from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";

type CandidateListActionsProps = {
  candidateId: string;
  candidateName: string;
  onGenerateCv: (id: string) => void;
  onDelete: (id: string, name: string) => void;
  onWorkflowChanged?: () => void;
  isGeneratingCv?: boolean;
};

/**
 * Candidates list row ⋯ menu: detail/CV/edit/delete plus workflow moves
 * (e.g. To New Contracts when the candidate is still in Intake).
 *
 * Every item is gated on the permission the API actually asks for, not on being able to see the
 * page. Five of the ten seeded roles can read candidates without being able to change or remove
 * one — the auditor, the case executive and the finance officer can do neither — and all of them
 * were being shown Edit and Delete, which failed at the server with a 403 the moment they were
 * used. An action nobody can take should not be on the menu.
 */
export function CandidateListActions({
  candidateId,
  candidateName,
  onGenerateCv,
  onDelete,
  onWorkflowChanged,
  isGeneratingCv,
}: CandidateListActionsProps) {
  const router = useRouter();
  const { hasPermission } = usePermissions();
  const canRead = hasPermission("candidate.read");
  const canUpdate = hasPermission("candidate.update");
  const canDelete = hasPermission("candidate.delete");
  const { actions, mutate } = useAvailableActions(candidateId);
  const [pendingRuleId, setPendingRuleId] = useState<string | null>(null);

  const workflowMoves = actions.filter((a) => a.isEnabled);

  const runTransition = async (transitionRuleId: string, label: string) => {
    setPendingRuleId(transitionRuleId);
    try {
      await executeTransition(candidateId, transitionRuleId);
      toast.success(`Executed: ${label}`);
      mutate();
      onWorkflowChanged?.();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Transition failed");
    } finally {
      setPendingRuleId(null);
    }
  };

  const busy = !!isGeneratingCv || !!pendingRuleId;

  // Nothing to offer: no trigger. A ⋯ that opens onto an empty panel reads as a broken row rather
  // than as a row this person is only meant to look at.
  if (!canRead && !canUpdate && !canDelete && workflowMoves.length === 0) return null;

  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          aria-label="Row actions"
          data-testid={`candidate-actions-${candidateId}`}
          disabled={busy}
        >
          {busy ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <MoreHorizontal className="h-4 w-4" />
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {workflowMoves.map((action) => (
          <DropdownMenuItem
            key={action.transitionRuleId}
            onClick={() =>
              runTransition(action.transitionRuleId, action.buttonLabel)
            }
          >
            <ArrowRight className="h-4 w-4 mr-2" />
            {action.buttonLabel}
          </DropdownMenuItem>
        ))}
        {workflowMoves.length > 0 ? <DropdownMenuSeparator /> : null}
        {canRead && (
          <>
            <DropdownMenuItem onClick={() => router.push(`/candidates/${candidateId}`)}>
              <Eye className="h-4 w-4 mr-2" /> View Details
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => onGenerateCv(candidateId)}>
              <FileText className="h-4 w-4 mr-2" /> Generate CV
            </DropdownMenuItem>
          </>
        )}
        {canUpdate && (
          <DropdownMenuItem onClick={() => router.push(`/candidates/${candidateId}/edit`)}>
            <Pencil className="h-4 w-4 mr-2" /> Edit
          </DropdownMenuItem>
        )}
        {canDelete && (
          <DropdownMenuItem
            onClick={() => onDelete(candidateId, candidateName)}
            className="text-destructive"
          >
            <Trash2 className="h-4 w-4 mr-2" /> Delete
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
