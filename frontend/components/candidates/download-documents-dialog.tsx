"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Download, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { downloadCandidateDocuments } from "@/lib/api/candidates";

/**
 * The document kinds, with the numbers the API's DocumentType enum uses.
 *
 * Named the way the desk names them rather than the way the enum does — "Passport", not
 * "Passport = 0". The order is the order the paperwork is usually assembled in.
 */
export const DOCUMENT_KINDS: { type: number; label: string; hint?: string }[] = [
  { type: 3, label: "CV", hint: "Generated if not already on file" },
  { type: 0, label: "Passport" },
  { type: 1, label: "Photo" },
  { type: 8, label: "Full size photo" },
  { type: 2, label: "Contract" },
  { type: 9, label: "Visa form" },
  { type: 5, label: "Medical certificate" },
  { type: 4, label: "LMIS document" },
  { type: 6, label: "Tasheer document" },
  { type: 7, label: "Ticket booking" },
  { type: 99, label: "Other" },
];

/** What an embassy run needs, and so what the dialog opens on. */
const DEFAULT_SELECTION = [3, 0, 1];

export function DownloadDocumentsDialog({
  open,
  onOpenChange,
  candidateIds,
  onDownloaded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  candidateIds: string[];
  onDownloaded?: () => void;
}) {
  const [selected, setSelected] = useState<number[]>(DEFAULT_SELECTION);
  const [downloading, setDownloading] = useState(false);

  const toggle = (type: number) =>
    setSelected((prev) =>
      prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type]
    );

  const count = candidateIds.length;

  const handleDownload = async () => {
    if (selected.length === 0 || count === 0) return;
    setDownloading(true);
    try {
      const blob = await downloadCandidateDocuments(candidateIds, selected);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `documents_${new Date().toISOString().slice(0, 10)}.zip`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 60_000);

      toast.success(
        `Downloaded documents for ${count} candidate${count === 1 ? "" : "s"}`
      );
      onDownloaded?.();
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Download failed");
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Download className="h-4 w-4 text-muted-foreground" />
            Download documents
          </DialogTitle>
          <DialogDescription>
            {count === 0
              ? "Select candidates first."
              : `One folder per candidate, for ${count} selected.`}
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[52vh] space-y-1 overflow-y-auto pr-1">
          {DOCUMENT_KINDS.map((kind) => (
            <label
              key={kind.type}
              className="flex cursor-pointer items-center gap-3 rounded-md px-2 py-2 hover:bg-muted/60"
            >
              <Checkbox
                checked={selected.includes(kind.type)}
                onCheckedChange={() => toggle(kind.type)}
                aria-label={kind.label}
              />
              <span className="flex-1">
                <span className="text-sm">{kind.label}</span>
                {kind.hint && (
                  <span className="block text-xs text-muted-foreground">{kind.hint}</span>
                )}
              </span>
            </label>
          ))}
        </div>

        <div className="flex items-center justify-between border-t pt-3">
          <Label className="text-xs text-muted-foreground">
            {selected.length} of {DOCUMENT_KINDS.length} selected
          </Label>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={() => setSelected(DOCUMENT_KINDS.map((k) => k.type))}
            >
              Select all
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={() => setSelected([])}
            >
              Clear
            </Button>
          </div>
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={downloading}
            onClick={() => onOpenChange(false)}
          >
            Cancel
          </Button>
          <Button
            type="button"
            disabled={downloading || selected.length === 0 || count === 0}
            onClick={handleDownload}
          >
            {downloading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Preparing…
              </>
            ) : (
              <>
                <Download className="mr-2 h-4 w-4" />
                Download
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
