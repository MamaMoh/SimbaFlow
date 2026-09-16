"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Download } from "lucide-react";
import { DownloadDocumentsDialog } from "@/components/candidates/download-documents-dialog";
import { usePermissions } from "@/lib/tenant/tenant-provider";

/**
 * "Download documents (n)" for a board's toolbar.
 *
 * Disabled rather than hidden when nothing is selected: a button that appears and disappears as you
 * tick boxes moves the rest of the toolbar around under the cursor, and hiding it also hides the
 * fact that batch download is available at all.
 *
 * Absent entirely without candidate.read, which is what the ZIP endpoint asks for. A board is
 * reached on its own permission — lmis.read, travel.read — and neither implies the right to read
 * the people on it, so the button has to ask for its own.
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
      <Button
        variant="outline"
        size="sm"
        className="h-8"
        disabled={count === 0}
        onClick={() => setOpen(true)}
      >
        <Download className="mr-1.5 h-3.5 w-3.5" />
        Download documents
        {count > 0 && <span className="ml-1 text-muted-foreground">({count})</span>}
      </Button>
      <DownloadDocumentsDialog
        open={open}
        onOpenChange={setOpen}
        candidateIds={candidateIds}
        onDownloaded={onDownloaded}
      />
    </>
  );
}
