"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { XIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { hereAndLooking } from "@/lib/ui/presence";

function Dialog({
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Root>) {
  return <DialogPrimitive.Root data-slot="dialog" {...props} />;
}

function DialogTrigger({
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Trigger>) {
  return <DialogPrimitive.Trigger data-slot="dialog-trigger" {...props} />;
}

function DialogPortal({
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Portal>) {
  return <DialogPrimitive.Portal data-slot="dialog-portal" {...props} />;
}

function DialogClose({
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Close>) {
  return <DialogPrimitive.Close data-slot="dialog-close" {...props} />;
}

function DialogOverlay({
  className,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Overlay>) {
  return (
    <DialogPrimitive.Overlay
      data-slot="dialog-overlay"
      className={cn(
        "data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 fixed inset-0 z-50 bg-black/50",
        className,
      )}
      {...props}
    />
  );
}

/**
 * Radix renders Select, Dropdown and Popover lists in their own portal, outside this dialog's DOM,
 * so picking an option reads as an interaction *outside* the dialog and dismisses it before the
 * value lands. Checking the event target does not help: the list has already unmounted by the time
 * the dismiss handler runs, so the target no longer says where the click began.
 *
 * Instead the pointerdown is recorded in the capture phase, while the list is still mounted.
 */
export function usePopperInteractionGuard() {
  const lastPopperDown = React.useRef(0);

  React.useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      const node = event.target as HTMLElement | null;
      if (
        node?.closest?.(
          "[data-radix-popper-content-wrapper],[data-slot='select-content'],[data-slot='select-item'],[role='listbox'],[role='option']",
        )
      ) {
        lastPopperDown.current = Date.now();
      }
    };
    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, []);

  // The dismiss fires on pointerup, a few milliseconds after the down it belongs to.
  return () => Date.now() - lastPopperDown.current < 500;
}

/**
 * Whether the person is actually here, looking at this page.
 *
 * A dialog closes when you click away from it, which is right — but only while you are in the
 * page to click away from. Going to another tab or another application to copy something is not
 * clicking away, and neither is the click that brings the window back: that one belongs to the
 * window, not to whatever happened to be under the pointer. Both used to take the form with
 * them, which on this app means going to another site for an E number and returning to an empty
 * board, with the typing gone and no way to tell what had happened.
 *
 * Deliberately not about this dialog's own focus. Nothing here asks where focus is inside the
 * page; it asks whether the page itself has it, which is the question the person is really
 * answering when they alt-tab away.
 */
export function useHereAndLooking() {
  const returnedAt = React.useRef(0);
  const wasAway = React.useRef(false);

  React.useEffect(() => {
    const left = () => {
      wasAway.current = true;
    };
    const back = () => {
      // Only counts as a return if we had actually gone. A plain window focus event with no
      // preceding blur would otherwise open the grace period for free.
      if (wasAway.current) returnedAt.current = Date.now();
      wasAway.current = false;
    };
    const onVisibility = () =>
      document.visibilityState === "hidden" ? left() : back();

    // Two different things, and both happen: switching tabs changes visibility without the
    // window losing focus, and switching applications loses focus without changing visibility.
    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("blur", left);
    window.addEventListener("focus", back);
    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("blur", left);
      window.removeEventListener("focus", back);
    };
  }, []);

  return () =>
    hereAndLooking({
      focused: document.hasFocus(),
      sinceReturn: returnedAt.current === 0 ? Infinity : Date.now() - returnedAt.current,
    });
}

/**
 * Whether anything has been typed into the dialog that is currently open.
 *
 * Reset every time a dialog mounts, which is what makes it per-dialog without a ref: the hook
 * lives and dies with the content it guards.
 */
function useTypedIn() {
  const typed = React.useRef(false);

  React.useEffect(() => {
    const note = (event: Event) => {
      const node = event.target as HTMLElement | null;
      if (node?.closest?.("[data-slot='dialog-content'],[data-slot='sheet-content']"))
        typed.current = true;
    };
    // Capture, because a form may stop these on the way up. `change` as well as `input`: a
    // select and a file picker announce themselves with nothing else.
    document.addEventListener("input", note, true);
    document.addEventListener("change", note, true);
    return () => {
      document.removeEventListener("input", note, true);
      document.removeEventListener("change", note, true);
    };
  }, []);

  return () => typed.current;
}

/**
 * @param dismissible Whether clicking away or pressing Escape closes it. Left true — the
 *   default — it always does. Set false it still does, right up until something is typed into
 *   the dialog: from then on a stray click on the page behind would throw that away, and there
 *   is nothing to undo a dismissal with, so it stops being one. Cancel and the X close it
 *   either way.
 *
 *   This used to mean "never", which protected a half-filled form at the cost of a dialog
 *   somebody had only opened to look at: clicking away did nothing, and the only way out was
 *   to find the X. Clicking away is how every other dialog in the app closes.
 */
function DialogContent({
  className,
  children,
  showCloseButton = true,
  dismissible = true,
  onPointerDownOutside,
  onInteractOutside,
  onEscapeKeyDown,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Content> & {
  showCloseButton?: boolean;
  dismissible?: boolean;
}) {
  const cameFromPopper = usePopperInteractionGuard();
  const typedIn = useTypedIn();
  const hereAndLooking = useHereAndLooking();

  /**
   * Reasons not to take this dismissal: there is work in here the caller asked to keep, the
   * click was really on a Select's list, or the person is not in the page at all.
   */
  const holdOpen = () =>
    (!dismissible && typedIn()) || cameFromPopper() || !hereAndLooking();

  return (
    <DialogPortal data-slot="dialog-portal">
      <DialogOverlay />
      <DialogPrimitive.Content
        data-slot="dialog-content"
        onPointerDownOutside={(event) => {
          if (holdOpen()) event.preventDefault();
          onPointerDownOutside?.(event);
        }}
        onInteractOutside={(event) => {
          if (holdOpen()) event.preventDefault();
          onInteractOutside?.(event);
        }}
        onEscapeKeyDown={(event) => {
          // Escape is unambiguous and comes from the keyboard of somebody who is here, so the
          // away guard has no part in it — only the unsaved-work one.
          if (!dismissible && typedIn()) event.preventDefault();
          onEscapeKeyDown?.(event);
        }}
        // A modal dialog has no business closing because focus moved — which is what a Select
        // does when it opens its list and again when it hands focus back.
        onFocusOutside={(event) => event.preventDefault()}
        className={cn(
          "bg-background data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 fixed top-[50%] left-[50%] z-50 grid w-full max-w-[calc(100%-2rem)] translate-x-[-50%] translate-y-[-50%] gap-4 rounded-lg border p-6 shadow-lg duration-200 sm:max-w-lg",
          className,
        )}
        {...props}
      >
        {children}
        {showCloseButton && (
          <DialogPrimitive.Close
            data-slot="dialog-close"
            className="ring-offset-background focus:ring-ring data-[state=open]:bg-accent data-[state=open]:text-muted-foreground absolute top-4 right-4 rounded-xs opacity-70 transition-opacity hover:opacity-100 focus:ring-2 focus:ring-offset-2 focus:outline-hidden disabled:pointer-events-none [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4"
          >
            <XIcon />
            <span className="sr-only">Close</span>
          </DialogPrimitive.Close>
        )}
      </DialogPrimitive.Content>
    </DialogPortal>
  );
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-header"
      className={cn("flex flex-col gap-2 text-center sm:text-left", className)}
      {...props}
    />
  );
}

function DialogFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-footer"
      className={cn(
        "flex flex-col-reverse gap-2 sm:flex-row sm:justify-end",
        className,
      )}
      {...props}
    />
  );
}

function DialogTitle({
  className,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Title>) {
  return (
    <DialogPrimitive.Title
      data-slot="dialog-title"
      className={cn("text-lg leading-none font-semibold", className)}
      {...props}
    />
  );
}

function DialogDescription({
  className,
  ...props
}: React.ComponentProps<typeof DialogPrimitive.Description>) {
  return (
    <DialogPrimitive.Description
      data-slot="dialog-description"
      className={cn("text-muted-foreground text-sm", className)}
      {...props}
    />
  );
}

export {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogOverlay,
  DialogPortal,
  DialogTitle,
  DialogTrigger,
};
