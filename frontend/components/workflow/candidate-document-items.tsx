"use client";

import { useState } from "react";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { FileText, FileSignature, Loader2, StickyNote } from "lucide-react";
import { toast } from "sonner";
import {
  generateCandidateContract,
  generateCandidateCv,
  generateCandidateVisaForm,
  visaFormGaps,
  type VisaFormGap,
} from "@/lib/api/candidates";
import { VisaDetailsDialog } from "@/components/workflow/visa-details-dialog";
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
  const [pending, setPending] = useState<Doc | null>(null);
  // What this candidate is still missing. Non-empty means the dialog is asking for it, and the
  // document is produced as soon as it is saved.
  const [gaps, setGaps] = useState<VisaFormGap[]>([]);

  // The API asks for candidate.read on all three — they are a rendering of the candidate, not a
  // separate thing to be granted. Offering an item that comes back 403 is worse than not offering it.
  if (!hasPermission("candidate.read") && !hasPermission("system.admin")) return null;

  const items = only ? DOCS.filter((d) => only.includes(d.key)) : DOCS;
  const missing = visaFormMissing ?? [];

  /**
   * The visa form asks for what it is missing rather than refusing because of it.
   *
   * The item used to grey itself out with "Visa form — details missing" and a tooltip naming
   * them, which told the desk what to do but not where: they opened the candidate, found the
   * field, typed it and came back. The same boxes are now one click away, here.
   */
  const runVisa = async () => {
    if (pending) return;
    setPending("visa");
    try {
      const found = (await visaFormGaps([candidateId])).filter((g) => g.fields.length > 0);
      if (found.length > 0) {
        setGaps(found);
        return;
      }
      saveFile(await generateCandidateVisaForm(candidateId));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Visa form failed");
    } finally {
      setPending(null);
    }
  };

  const run = async (doc: (typeof DOCS)[number]) => {
    if (doc.key === "visa") return runVisa();
    if (pending) return;
    setPending(doc.key);
    try {
      // Saved rather than opened in a tab. A tab goes straight to the print dialog, which is how
      // these are used most of the time — but a blob URL has no filename, so the one document the
      // desk does keep arrives in Downloads named after the URL's identifier and is indistinguishable
      // from every other one. A saved file is named after the candidate and is still one double-click
      // from the same print dialog.
      saveFile(await doc.generate(candidateId));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : `${doc.label} failed`);
    } finally {
      setPending(null);
    }
  };

  const incomplete = (doc: Doc) => doc === "visa" && missing.length > 0;

  return (
    <>
      <VisaDetailsDialog
        open={gaps.length > 0}
        gaps={gaps}
        onOpenChange={(next) => {
          if (!next) {
            setGaps([]);
            setPending(null);
          }
        }}
        onFilled={() => {
          setGaps([]);
          void (async () => {
            try {
              saveFile(await generateCandidateVisaForm(candidateId));
            } catch (err) {
              toast.error(err instanceof Error ? err.message : "Visa form failed");
            } finally {
              setPending(null);
            }
          })();
        }}
      />

      {items.map((doc) => (
        <DropdownMenuItem
          key={doc.key}
          disabled={pending !== null}
          // Live, not greyed out: pressing it is how the missing details get filled in. The
          // label still says there are some, so nobody expects a finished form.
          title={incomplete(doc.key) ? `Still needed: ${missing.join(", ")}` : undefined}
          onSelect={(e) => {
            // The menu would close on select and unmount the row before the PDF arrives, taking
            // the spinner and the error toast's context with it.
            e.preventDefault();
            void run(doc);
          }}
        >
          {pending === doc.key ? (
            <Loader2 className="mr-2 h-4 w-4 shrink-0 animate-spin" />
          ) : (
            <doc.icon className="mr-2 h-4 w-4 shrink-0" />
          )}
          {incomplete(doc.key) ? "Visa form — fill in details" : doc.label}
        </DropdownMenuItem>
      ))}
    </>
  );
}
