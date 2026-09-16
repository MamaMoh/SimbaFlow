"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Download } from "lucide-react";
import { DownloadDocumentsDialog } from "@/components/candidates/download-documents-dialog";

/**
 * "Download documents (n)" for a board's toolbar.
 *
 * Disabled rather than hidden when nothing is selected: a button that appears and disappears as you
 * tick boxes moves the rest of the toolbar around under the cursor, and hiding it also hides the
 * fact that batch download is available at all.
 */
export function BulkDownloadButton({
  candidateIds,
  onDownloaded,
}: {
  candidateIds: string[];
  onDownloaded?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const count = candidateIds.length;

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
