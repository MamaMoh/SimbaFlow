"use client";

import { useEffect } from "react";

/**
 * Safety net for a well-known Radix bug: opening a Dialog/Sheet from inside a DropdownMenu can
 * leave `document.body { pointer-events: none }` behind, which freezes the entire page — every
 * click is swallowed — until a reload. Users read that as "the site is stuck".
 *
 * This clears the stuck style whenever no Radix modal is actually open, so the page recovers on its
 * own within a moment or on the next mouse move instead of needing a refresh. The per-menu
 * `modal={false}` fixes the cause; this guarantees no missed spot can wedge the app.
 */
export function PointerEventsGuard() {
  useEffect(() => {
    const clearIfStuck = () => {
      if (document.body.style.pointerEvents !== "none") return;
      const modalOpen = document.querySelector(
        '[data-state="open"][role="dialog"], [role="menu"][data-state="open"], [data-radix-popper-content-wrapper]'
      );
      if (!modalOpen) document.body.style.pointerEvents = "";
    };
    const id = window.setInterval(clearIfStuck, 400);
    window.addEventListener("pointermove", clearIfStuck, { passive: true });
    return () => {
      window.clearInterval(id);
      window.removeEventListener("pointermove", clearIfStuck);
    };
  }, []);

  return null;
}
