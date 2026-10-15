"use client";

import { useMemo, useState } from "react";
import { ClipboardPaste, TriangleAlert } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { SITE_MANIFESTS, type FieldKey } from "@/lib/site-bridge/manifest";
import { bestRead } from "@/lib/site-bridge/read";

/**
 * Registering somebody who is already on the portal.
 *
 * The desk has the page open in another tab and retypes twenty-odd boxes out of it, which is
 * slow and is where a transposed passport number comes from. Here the page is copied whole and
 * pasted once, and the labels on it do the rest.
 *
 * A recognised page fills on the paste itself. There is nothing to decide at that point: the
 * dialog's preview was the same list of values the form is about to show, and the form is where
 * anything wrong gets corrected anyway, so a confirm step only asked the desk to read the same
 * twenty values twice. The toast says how many went in and the form stays unsaved.
 *
 * The preview is still here for the page that is not recognised on sight — a partial copy, or a
 * page pasted in pieces. It reports three things and they are all worth showing: what it
 * recognised, what the page did not carry, and what the page carried that we have no field for —
 * that last list is how a label we have not mapped yet gets noticed rather than silently dropped.
 *
 * No extension, on purpose. This works in any browser, on a phone, and on a page pasted out of a
 * PDF or a message from the office in Riyadh — none of which an extension reaches.
 */
export function PasteFromPortal({
  open,
  onOpenChange,
  onApply,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Canonical values. The form they land in decides how to spell them for itself. */
  onApply: (values: Partial<Record<FieldKey, string>>) => void;
}) {
  const [text, setText] = useState("");

  const read = useMemo(
    () => (text.trim().length > 0 ? bestRead(text, SITE_MANIFESTS) : null),
    [text],
  );

  const apply = (values: Partial<Record<FieldKey, string>>) => {
    onApply(values);
    setText("");
    onOpenChange(false);
  };

  /**
   * The paste is the instruction. Reading the clipboard here rather than waiting for the
   * textarea lets a page that is recognised go straight into the form, and only the paste —
   * not a keystroke, and not a second paste appended to a first — is treated that way, so
   * nothing fills while somebody is still assembling the text by hand.
   */
  const onPaste = (e: React.ClipboardEvent<HTMLTextAreaElement>) => {
    if (text.length > 0) return;
    const pasted = e.clipboardData.getData("text");
    const straightIn = pasted.trim().length > 0 ? bestRead(pasted, SITE_MANIFESTS) : null;
    if (!straightIn) return;
    e.preventDefault();
    apply(straightIn.result.values);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) setText("");
        onOpenChange(next);
      }}
    >
      {/* The one dialog in the app that exists to be used while the person is somewhere else:
          they are on the portal, copying. Leaving the page must not take this with it. */}
      <DialogContent dismissible={false} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Paste from the portal</DialogTitle>
          <DialogDescription>
            Select the whole page on the portal, copy it, and paste it here. The labels on the
            page are what is read, and a page we recognise fills the form as soon as you paste
            it — check it over before you save.
          </DialogDescription>
        </DialogHeader>

        <Textarea
          autoFocus
          value={text}
          onPaste={onPaste}
          onChange={(e) => setText(e.target.value)}
          placeholder="Paste the copied page here"
          className="min-h-32 font-mono text-xs"
        />

        {text.trim().length > 0 && !read && (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <TriangleAlert className="size-4 shrink-0" />
            Nothing here looks like a portal page. Copy the whole page rather than one field.
          </p>
        )}

        {read && (
          <div className="max-h-72 space-y-4 overflow-y-auto pr-1">
            <div className="flex items-center gap-2">
              <Badge variant="secondary">{read.manifest.title}</Badge>
              <span className="text-muted-foreground text-sm">
                {read.result.found.length} fields recognised
              </span>
            </div>

            <dl className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)] gap-x-4 gap-y-1 text-sm">
              {read.result.found.map((f) => (
                <div key={f.key} className="contents">
                  <dt className="text-muted-foreground truncate">{f.label}</dt>
                  <dd className="truncate font-medium" dir="auto">
                    {f.value}
                  </dd>
                </div>
              ))}
            </dl>

            {read.result.missing.length > 0 && (
              <p className="text-muted-foreground text-xs">
                Not on the page — {read.result.missing.map((m) => m.label).join(", ")}
              </p>
            )}

            {read.result.unrecognised.length > 0 && (
              <p className="text-muted-foreground text-xs">
                Read but not used — {read.result.unrecognised.map((u) => u.label).join(", ")}
              </p>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => read && apply(read.result.values)} disabled={!read}>
            <ClipboardPaste className="size-4" />
            Fill the form
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
