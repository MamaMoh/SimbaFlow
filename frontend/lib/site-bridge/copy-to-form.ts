import { encodePayload, toPayload, type PlanResponse } from "./clipboard";

/**
 * The first extension version that fills a form when a payload is pasted into it. Older ones
 * fill from the toolbar only, and pasting into them drops a line of base64 into a box — so the
 * app says so up front rather than letting someone find out on the portal.
 */
export const PASTE_HELPER_VERSION = "0.8.0";

/** Which helper is installed, as the extension marks it on this page; null when there is none. */
export function installedHelper(): string | null {
  if (typeof document === "undefined") return null;
  return document.documentElement.dataset.simbaflowHelper ?? null;
}

/** "0.7.0" against "0.8.0", numerically, so 0.10 is newer than 0.9. */
export function atLeast(version: string, minimum: string): boolean {
  const a = version.split(".").map((n) => parseInt(n, 10) || 0);
  const b = minimum.split(".").map((n) => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(a.length, b.length); i += 1) {
    if ((a[i] ?? 0) !== (b[i] ?? 0)) return (a[i] ?? 0) > (b[i] ?? 0);
  }
  return true;
}

async function fetchPlan(site: string, candidateId: string): Promise<PlanResponse> {
  const res = await fetch(
    `/api/site-maps/plan?site=${encodeURIComponent(site)}&candidateId=${encodeURIComponent(candidateId)}`,
    { credentials: "same-origin", cache: "no-store" },
  );
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.data) {
    throw new Error(body?.error || `Could not prepare the form (${res.status})`);
  }
  return body.data as PlanResponse;
}

/**
 * Puts one candidate on the clipboard as a fill for `site`, and says who it was.
 *
 * The plan is fetched inside the clipboard write rather than before it. A browser only lets a
 * page write to the clipboard while the click that asked for it is still fresh, and a round trip
 * to the server is long enough for that to lapse — so the write is started at once and handed
 * the fetch to wait on, which Chrome and Edge allow. Where that form is not supported the fetch
 * comes first and the plain write follows, which works whenever the server is quick.
 */
export async function copyCandidateForForm(
  site: string,
  candidateId: string,
): Promise<PlanResponse> {
  const plan = fetchPlan(site, candidateId);
  const text = plan.then((r) => encodePayload(toPayload(r)));

  try {
    if (typeof ClipboardItem !== "undefined" && navigator.clipboard?.write) {
      await navigator.clipboard.write([
        new ClipboardItem({
          "text/plain": text.then((t) => new Blob([t], { type: "text/plain" })),
        }),
      ]);
    } else {
      await navigator.clipboard.writeText(await text);
    }
  } catch (err) {
    // A failed fetch fails the write as well, as a clipboard error that says nothing useful.
    // Surface the fetch's own reason when it has one.
    await plan;
    throw err;
  }

  return plan;
}
