"use client";

import { useState } from "react";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { Download } from "lucide-react";
import { DownloadDocumentsDialog } from "@/components/candidates/download-documents-dialog";
import { usePermissions } from "@/lib/tenant/tenant-provider";

/**
 * "Candidate documents (n)" for a board's Export menu.
 *
 * Was a button of its own sitting next to the one called Export, which is two names for taking
 * something off a board. It is an item under Export now, and the toolbar is a button shorter on
 * every board that has one.
 *
 * Disabled rather than hidden when nothing is ticked: an item that comes and goes as you tick
 * boxes moves the rest of the menu under the cursor, and hiding it also hides the fact that
 * downloading the paperwork in a batch is possible at all.
 *
 * Absent entirely without candidate.read, which is what the ZIP endpoint asks for. A board is
 * reached on its own permission — lmis.read, travel.read — and neither implies the right to read
 * the people on it, so this has to ask for its own.
 */
export function BulkDownloadButton({
  candidateIds,
  onDownloaded,
}: {
  candidateIds: string[];
  onDownloaded?: () => void;
}) {
  const { hasPermission } = usePermissions();
  const [open, setOpen] = useState(false);
  const count = candidateIds.length;

  if (!hasPermission("candidate.read")) return null;

  return (
    <>
      <DropdownMenuItem
        disabled={count === 0}
        // The menu closes on the click that opens the dialog, and Radix returns focus to the
        // trigger as it goes — which would take it off the dialog the moment it appeared.
        onSelect={(e) => {
          e.preventDefault();
          setOpen(true);
        }}
      >
        <Download className="mr-2 h-4 w-4" />
        {count > 0 ? `Candidate documents (${count})` : "Candidate documents"}
      </DropdownMenuItem>
      <DownloadDocumentsDialog
        open={open}
        onOpenChange={setOpen}
        candidateIds={candidateIds}
        onDownloaded={onDownloaded}
      />
    </>
  );
}
