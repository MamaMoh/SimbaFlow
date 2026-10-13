"use client";

import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { FileText, FileSignature, StickyNote } from "lucide-react";
import { toast } from "sonner";
import {
  generateCandidateContract,
  generateCandidateCv,
  generateCandidateVisaForm,
  visaFormGaps,
} from "@/lib/api/candidates";
import { useVisaDetails } from "@/components/workflow/visa-details-host";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { saveFile } from "@/lib/files/download";

type Doc = "cv" | "visa" | "contract";

const DOCS: {
  key: Doc;
  label: string;
  icon: typeof FileText;
  generate: (id: string) => Promise<File>;
}[] = [
  { key: "cv", label: "Download CV", icon: FileText, generate: generateCandidateCv },
  { key: "visa", label: "Download visa form", icon: StickyNote, generate: generateCandidateVisaForm },
  {
    key: "contract",
    label: "Download contract",
    icon: FileSignature,
    generate: generateCandidateContract,
  },
];

/**
 * The candidate's printable paperwork, as items inside a board row's ⋯ menu.
 *
 * Every stage board had the same gap: the CV could only be reached from the Candidates list or the
 * candidate's own page, so anyone working a board had to leave it, print, and come back. The
 * paperwork is the same wherever the candidate happens to be standing in the pipeline, so the items
 * are the same too.
 *
 * Nothing here renders a dialog or holds a spinner, and that is deliberate. A dropdown unmounts
 * its children when it closes, so anything these items put on screen had to be paid for by
 * holding the menu open — which left a menu sitting over the form it had just opened. Progress
 * goes to a toast and the missing-details form goes to the app-level host, both of which outlive
 * the menu, so the menu can close the instant something is picked, like every other item in the app.
 */
export function CandidateDocumentItems({
  candidateId,
  only,
  visaFormMissing,
}: {
  candidateId: string;
  /** Limit to certain documents; defaults to all three. */
  only?: Doc[];
  /**
   * What the enjaze form is still waiting for, where the caller already knows. It only changes
   * the label, so the desk can see before clicking that there is typing to do. The item is live
   * either way, and pressing it checks for itself — a board that does not carry visa details on
   * its rows gets the same behaviour, one request later.
   */
  visaFormMissing?: string[];
}) {
  const { hasPermission } = usePermissions();
  const askForVisaDetails = useVisaDetails();

  // The API asks for candidate.read on all three — they are a rendering of the candidate, not a
  // separate thing to be granted. Offering an item that comes back 403 is worse than not offering it.
  if (!hasPermission("candidate.read") && !hasPermission("system.admin")) return null;

  const items = only ? DOCS.filter((d) => only.includes(d.key)) : DOCS;
  const missing = visaFormMissing ?? [];

  /**
   * Saved rather than opened in a tab. A tab goes straight to the print dialog, which is how
   * these are used most of the time — but a blob URL has no filename, so the one document the
   * desk does keep arrives in Downloads named after the URL's identifier and is indistinguishable
   * from every other one. A saved file is named after the candidate and is still one double-click
   * from the same print dialog.
   */
  const download = async (label: string, generate: () => Promise<File>) => {
    // The toast is the progress indicator now. It belongs to the page rather than to the menu,
    // so it survives the menu closing and is still there when the PDF lands a second later.
    const id = toast.loading(`${label}…`);
    try {
      saveFile(await generate());
      toast.success(label, { id });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : `${label} failed`, { id });
    }
  };

  /**
   * The visa form asks for what it is missing rather than refusing because of it.
   *
   * The item used to grey itself out with "Visa form — details missing" and a tooltip naming
   * them, which told the desk what to do but not where: they opened the candidate, found the
   * field, typed it and came back. The same boxes are now one click away, here.
   */
  const runVisa = async () => {
    let found;
    try {
      found = (await visaFormGaps([candidateId])).filter((g) => g.fields.length > 0);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Visa form failed");
      return;
    }

    if (found.length > 0) {
      askForVisaDetails({
        gaps: found,
        onFilled: () =>
          void download("Visa form", () => generateCandidateVisaForm(candidateId)),
      });
      return;
    }

    await download("Visa form", () => generateCandidateVisaForm(candidateId));
  };

  const incomplete = (doc: Doc) => doc === "visa" && missing.length > 0;

  return (
    <>
      {items.map((doc) => (
        <DropdownMenuItem
          key={doc.key}
          // Live, not greyed out: pressing it is how the missing details get filled in. The
          // label still says there are some, so nobody expects a finished form.
          title={incomplete(doc.key) ? `Still needed: ${missing.join(", ")}` : undefined}
          onSelect={() => {
            // No preventDefault. The menu closes, which is the whole point — what it used to be
            // held open for now lives outside it.
            if (doc.key === "visa") void runVisa();
            else void download(doc.label, () => doc.generate(candidateId));
          }}
        >
          <doc.icon className="mr-2 h-4 w-4 shrink-0" />
          {incomplete(doc.key) ? "Visa form — fill in details" : doc.label}
        </DropdownMenuItem>
      ))}
    </>
  );
}
