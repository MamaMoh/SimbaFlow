import type { FillPlan } from "./write";

/**
 * A candidate on the clipboard, ready to be pasted into somebody else's form.
 *
 * The other way in to the extension. The toolbar button asks who the form is for and searches;
 * this lets the person choose on the board they are already looking at — right-click a row,
 * Copy to insurance — and then paste into the portal like they paste anything else. The
 * extension sees the paste, recognises it as one of these, and fills the form instead of
 * dropping text into whichever box had the cursor.
 *
 * What travels is the finished plan, not the candidate. The extension has no business knowing
 * what a sponsor is, and this keeps it that way: it receives exactly what the toolbar route
 * would have fetched, only by a different road.
 *
 * The format is one line, a fixed prefix and base64 JSON, and the extension decodes it with its
 * own twenty lines of plain JavaScript (`extension/paste.js`). Change one, change the other —
 * the version number in the prefix is what lets an old extension refuse a newer payload rather
 * than misread it.
 */

export const CLIPBOARD_PREFIX = "simbaflow-fill:1:";

/**
 * How long a copy is good for.
 *
 * A paste fills a government form with whoever was copied, and the clipboard outlives the moment
 * it was filled — copy Hiwot at nine, paste at four, and Hiwot goes into the form that was meant
 * for somebody else. Long enough to walk a batch of twenty, short enough that a forgotten copy
 * stops working before it does harm.
 */
export const CLIPBOARD_LIFETIME_MS = 20 * 60 * 1000;

export type ClipboardPayload = {
  site: { id: string; title: string };
  candidate: { id: string; name: string; passportNumber: string };
  plan: FillPlan;
  /**
   * The labels this form prints, for the extension to check the page against before it touches
   * anything. Pasting the insurance payload into a webmail compose box should do nothing at all,
   * and the only way to know that page is not the form is to look for the form's own words.
   */
  recognise: string[];
  expiresAt: number;
};

/** The plan route's answer, which is everything a payload is made of. */
export type PlanResponse = {
  site: { id: string; title: string };
  candidate: { id: string; name: string; passportNumber: string };
  plan: FillPlan;
};

export function toPayload(response: PlanResponse, now = Date.now()): ClipboardPayload {
  const recognise = Array.from(
    new Set([...response.plan.steps, ...response.plan.missing].map((s) => s.label)),
  );
  return {
    site: { id: response.site.id, title: response.site.title },
    candidate: response.candidate,
    plan: response.plan,
    recognise,
    expiresAt: now + CLIPBOARD_LIFETIME_MS,
  };
}

/**
 * Base64 over UTF-8, because the names are not all ASCII — an Amharic local name or an Arabic
 * sponsor name goes through `btoa` alone as an exception.
 */
export function encodePayload(payload: ClipboardPayload): string {
  const bytes = new TextEncoder().encode(JSON.stringify(payload));
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return CLIPBOARD_PREFIX + btoa(binary);
}

/** The inverse, for tests and for anything in the app that wants to read one back. */
export function decodePayload(text: string): ClipboardPayload | null {
  const trimmed = text.trim();
  if (!trimmed.startsWith(CLIPBOARD_PREFIX)) return null;
  try {
    const binary = atob(trimmed.slice(CLIPBOARD_PREFIX.length));
    const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as ClipboardPayload;
  } catch {
    return null;
  }
}
