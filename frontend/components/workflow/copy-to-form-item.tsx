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
import { INSURANCE_MEMBER, LMIS_CONTRACT } from "@/lib/site-bridge/manifest";

/**
 * The forms a row can be copied for, and how each is spoken of.
 *
 * Insurance is walked a batch at a time — right-click, copy, switch tab, paste, check, Add
 * Member, twenty times. LMIS is one contract per labourer, opened from the labourer's own record,
 * so the paste goes into the Create Contract dialog that is already on their screen.
 */
const FORMS = {
  insurance: {
    site: INSURANCE_MEMBER.id,
    menu: "Copy to insurance",
    copied: "copied for insurance",
    where: "Click into the Add Insured form and paste. Every box fills at once.",
    portal: "the insurance form",
  },
  lmis: {
    site: LMIS_CONTRACT.id,
    menu: "Copy to LMIS contract",
    copied: "copied for the LMIS contract",
    where: "Open Create Contract on their LMIS record, click into it and paste. The boxes fill at once.",
    portal: "the LMIS Create Contract form",
  },
} as const;

export type CopyTarget = keyof typeof FORMS;

/**
 * Copy to insurance, Copy to LMIS contract — on any row menu that holds a candidate.
 *
 * Puts the candidate on the clipboard as a finished fill for that form. Pasting it there, with
 * the browser helper installed, fills every box at once.
 *
 * One component for every form, because the toast is the part that matters and it should say
 * the same thing everywhere: without the helper the paste is a line of gibberish in the first
 * box, and the person deserves to hear that here rather than find it there.
 */
export function CopyToFormItem({
  form,
  candidateId,
  candidateName,
}: {
  form: CopyTarget;
  candidateId: string;
  candidateName: string;
}) {
  const { hasPermission } = usePermissions();
  // The plan is built from the candidate record, which is read under this permission.
  if (!hasPermission("candidate.read")) return null;
  const target = FORMS[form];

  const copy = async () => {
    try {
      await copyCandidateForForm(target.site, candidateId);
      const helper = installedHelper();
      const who = candidateName || "Candidate";
      if (helper && atLeast(helper, PASTE_HELPER_VERSION)) {
        toast.success(`${who} ${target.copied}`, { description: target.where });
      } else {
        toast.warning(`${who} copied, but pasting needs the browser helper`, {
          description: helper
            ? `Version ${helper} is installed and filling on paste needs ${PASTE_HELPER_VERSION}. Update it from Settings → Browser helper.`
            : `Install it from Settings → Browser helper, then paste into ${target.portal}.`,
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
      {target.menu}
    </DropdownMenuItem>
  );
}
