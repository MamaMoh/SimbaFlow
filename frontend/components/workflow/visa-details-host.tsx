"use client";

import { createContext, useCallback, useContext, useRef, useState } from "react";
import { VisaDetailsDialog } from "@/components/workflow/visa-details-dialog";
import type { VisaFormGap } from "@/lib/api/candidates";

type Ask = {
  gaps: VisaFormGap[];
  /** What was waiting on the details. Not called when the person only saves. */
  onFilled: () => void;
  /** Saved without printing — let whoever asked drop its spinner. */
  onCancelled?: () => void;
  /**
   * Whether every box has to be filled before it will save.
   *
   * True when something is waiting on these — a form that cannot print without them, which is
   * why the dialog opened. False when somebody opened it themselves to put a value right: the
   * contract page off the portal carries four of these and sometimes three, and refusing to
   * save three because the fourth was not printed is refusing the typing that was done.
   */
  requireAll?: boolean;
};

const AskForVisaDetails = createContext<(ask: Ask) => void>(() => {});

/**
 * One visa-details dialog for the whole application, mounted above every page.
 *
 * It used to be rendered by whichever thing needed it, and one of those was a row's ⋯ menu.
 * A dropdown unmounts its children the moment it closes, so the menu had to be held open for
 * the dialog to survive — which is why a form and the menu that opened it were on screen
 * together, the menu sitting over the boxes somebody was trying to type into.
 *
 * Hoisting the dialog out of the menu is what fixes that rather than any amount of z-index:
 * the menu closes on select like every other item in the app, and the dialog is not its child
 * so it stays. Only one can be open at a time anywhere, which is why one host serves everything.
 */
export function VisaDetailsHost({ children }: { children: React.ReactNode }) {
  // Whether it is on screen is kept apart from what is in it, because the dialog fades out
  // rather than vanishing. Clearing both at once empties the boxes while the fade is still
  // running, so for a third of a second the form reads "this candidate still needs these" with
  // nothing under it. The content stays until the next thing asks for the form.
  const [open, setOpen] = useState(false);
  const [ask, setAsk] = useState<Ask | null>(null);

  // Read by the callbacks below so they can stay stable. A state updater is not the place to
  // call somebody else's callback from — React is free to run an updater more than once.
  const latest = useRef<Ask | null>(null);

  // Stable, so a board that passes it into a dependency array does not re-run on every render.
  const request = useCallback((next: Ask) => {
    latest.current = next;
    setAsk(next);
    setOpen(true);
  }, []);

  /** Closed with nothing printed — dismissed, or saved by the Save button. */
  const dismiss = useCallback(() => {
    setOpen(false);
    latest.current?.onCancelled?.();
  }, []);

  return (
    <AskForVisaDetails.Provider value={request}>
      {children}
      <VisaDetailsDialog
        open={open}
        gaps={ask?.gaps ?? []}
        requireAll={ask?.requireAll ?? true}
        onOpenChange={(next) => {
          if (!next) dismiss();
        }}
        onFilled={(print) => {
          if (!print) return dismiss();
          setOpen(false);
          latest.current?.onFilled();
        }}
      />
    </AskForVisaDetails.Provider>
  );
}

/**
 * Ask for whatever the enjaze form is missing, then carry on.
 *
 * The caller hands over the gaps and what to do once they are filled in. Nothing is rendered
 * where the caller stands, which is the point — it works from inside a dropdown menu.
 */
export function useVisaDetails() {
  return useContext(AskForVisaDetails);
}
