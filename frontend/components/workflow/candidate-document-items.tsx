"use client";

import { useState } from "react";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { FileText, FileSignature, Loader2, StickyNote } from "lucide-react";
import { toast } from "sonner";
import {
  generateCandidateContract,
  generateCandidateCv,
  generateCandidateVisaForm,
} from "@/lib/api/candidates";
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
   * What the enjaze form is still waiting for, where the caller knows. Given, the visa item is
   * disabled and says so; omitted, it stays live and the API's refusal names the gaps instead —
   * boards that do not carry visa details on their rows are not made to fetch them.
   */
  visaFormMissing?: string[];
}) {
  const { hasPermission } = usePermissions();
  const [pending, setPending] = useState<Doc | null>(null);

  // The API asks for candidate.read on all three — they are a rendering of the candidate, not a
  // separate thing to be granted. Offering an item that comes back 403 is worse than not offering it.
  if (!hasPermission("candidate.read") && !hasPermission("system.admin")) return null;

  const items = only ? DOCS.filter((d) => only.includes(d.key)) : DOCS;
  const missing = visaFormMissing ?? [];
  const blocked = (doc: Doc) => doc === "visa" && missing.length > 0;

  const run = async (doc: (typeof DOCS)[number]) => {
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

  return (
    <>
      {items.map((doc) => (
        <DropdownMenuItem
          key={doc.key}
          disabled={pending !== null || blocked(doc.key)}
          title={blocked(doc.key) ? `Still needed: ${missing.join(", ")}` : undefined}
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
          {blocked(doc.key) ? "Visa form — details missing" : doc.label}
        </DropdownMenuItem>
      ))}
    </>
  );
}
