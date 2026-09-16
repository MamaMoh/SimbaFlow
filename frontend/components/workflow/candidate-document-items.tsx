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

/**
 * Opens a generated PDF in a new tab rather than saving it.
 *
 * These are printed far more often than they are filed, and a tab goes straight to the print
 * dialog; a download makes the desk find the file first. The object URL is released on a timer
 * because revoking it immediately races the tab that is still loading it.
 */
function openPdf(blob: Blob) {
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener,noreferrer");
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

type Doc = "cv" | "visa" | "contract";

const DOCS: {
  key: Doc;
  label: string;
  icon: typeof FileText;
  generate: (id: string) => Promise<Blob>;
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
}: {
  candidateId: string;
  /** Limit to certain documents; defaults to all three. */
  only?: Doc[];
}) {
  const { hasPermission } = usePermissions();
  const [pending, setPending] = useState<Doc | null>(null);

  // The API asks for candidate.read on all three — they are a rendering of the candidate, not a
  // separate thing to be granted. Offering an item that comes back 403 is worse than not offering it.
  if (!hasPermission("candidate.read") && !hasPermission("system.admin")) return null;

  const items = only ? DOCS.filter((d) => only.includes(d.key)) : DOCS;

  const run = async (doc: (typeof DOCS)[number]) => {
    if (pending) return;
    setPending(doc.key);
    try {
      openPdf(await doc.generate(candidateId));
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
          disabled={pending !== null}
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
          {doc.label}
        </DropdownMenuItem>
      ))}
    </>
  );
}
