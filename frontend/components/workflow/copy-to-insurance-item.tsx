"use client";

import { ClipboardCopy } from "lucide-react";
import { toast } from "sonner";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  PASTE_HELPER_VERSION,
  atLeast,
  copyCandidateForForm,
  installedHelper,
} from "@/lib/site-bridge/copy-to-form";
import { INSURANCE_MEMBER } from "@/lib/site-bridge/manifest";

/**
 * Copy to insurance, on any row menu that holds a candidate.
 *
 * Puts the candidate on the clipboard as a fill for the insurance portal's Add Insured form.
 * Pasting it there, with the browser helper installed, fills every box at once — so walking a
 * batch is right-click, copy, switch tab, paste, check, Add Member, twenty times, instead of
 * sixteen boxes typed twenty times.
 *
 * One component rather than an item per board, because the toast is the part that matters and
 * it should say the same thing everywhere: without the helper the paste is a line of gibberish
 * in the First Name box, and the person deserves to hear that here rather than find it there.
 */
export function CopyToInsuranceItem({
  candidateId,
  candidateName,
}: {
  candidateId: string;
  candidateName: string;
}) {
  const { hasPermission } = usePermissions();
  // The plan is built from the candidate record, which is read under this permission.
  if (!hasPermission("candidate.read")) return null;

  const copy = async () => {
    try {
      await copyCandidateForForm(INSURANCE_MEMBER.id, candidateId);
      const helper = installedHelper();
      const who = candidateName || "Candidate";
      if (helper && atLeast(helper, PASTE_HELPER_VERSION)) {
        toast.success(`${who} copied for insurance`, {
          description: "Click into the Add Insured form and paste. Every box fills at once.",
        });
      } else {
        toast.warning(`${who} copied, but pasting needs the browser helper`, {
          description: helper
            ? `Version ${helper} is installed and filling on paste needs ${PASTE_HELPER_VERSION}. Update it from Settings → Browser helper.`
            : "Install it from Settings → Browser helper, then paste into the insurance form.",
        });
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not copy the candidate");
    }
  };

  return (
    // onSelect runs inside the click that chose it, which is what lets the page write to the
    // clipboard at all.
    <DropdownMenuItem onSelect={() => void copy()}>
      <ClipboardCopy className="mr-2 h-4 w-4" />
      Copy to insurance
    </DropdownMenuItem>
  );
}
