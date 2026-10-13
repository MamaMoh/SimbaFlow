/**
 * How long after the page comes back to the front a click is still read as "come back to this
 * window" rather than as "dismiss this".
 *
 * Clicking a background window delivers the focus and the click as one gesture, microseconds
 * apart. A quarter of a second is far longer than that and far shorter than the pause before
 * somebody aims a second, deliberate click.
 */
export const RETURNING_MS = 250;

/**
 * Whether the person is actually here, looking at this page — the question a dialog has to
 * answer before it treats a click as "clicked away".
 *
 * Going to another tab or another application to copy something is not clicking away, and
 * neither is the click that brings the window back: that one belongs to the window, not to
 * whatever happened to be under the pointer.
 *
 * Focus, not visibility. Both go false when you switch tabs or applications, so either would
 * do for that — but visibility also reports hidden for a page being rendered somewhere other
 * than a front tab, and a page like that is still being clicked in. Gating on visibility would
 * leave such a page unable to dismiss anything at all, which is a worse failure than the one
 * being fixed. A document that is being clicked in has focus by definition.
 */
export function hereAndLooking(state: {
  /** document.hasFocus() */
  focused: boolean;
  /** Milliseconds since the page last came back to the front; Infinity if it never left. */
  sinceReturn: number;
}): boolean {
  return state.focused && state.sinceReturn > RETURNING_MS;
}
