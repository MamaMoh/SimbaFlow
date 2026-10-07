/**
 * Saving a generated document under a name a person can read.
 *
 * Every one of these downloads used to arrive named after something only the machine cared about.
 * A CV opened in a browser tab and then saved landed in Downloads as the object URL's identifier —
 * b5e6ecb2-041c-41e7-9865-eca1908e5cac.pdf — because a blob URL has no filename for the viewer to
 * offer; the bundle came out as documents_2026-10-07.pdf, which at least says the day but not the
 * candidate. A desk printing paperwork for twenty people in a morning ends up unable to tell any
 * of them apart without opening each one.
 *
 * The API names these now (see DocumentFileName on the server) and sends it in Content-Disposition.
 * `readPdfBlob` attaches that name to the Blob it returns — a File *is* a Blob, so every existing
 * caller is unaffected — and `saveFile` uses it. The fallback is for the responses that have no
 * single candidate to be named after, such as a bulk export.
 */

/**
 * The filename the server asked for, or `fallback`.
 *
 * Handles both forms of the header: `filename="..."` and the RFC 5987 `filename*=UTF-8''...` that
 * ASP.NET Core sends whenever the name is not plain ASCII — which is any candidate whose name is
 * written in Amharic. The encoded form is preferred when both are present, which is what the RFC
 * requires and also the only one of the two that is correct for those names.
 */
export function filenameFromResponse(res: Response, fallback: string): string {
  const header = res.headers.get("content-disposition");
  if (!header) return fallback;

  const encoded = /filename\*=\s*UTF-8''([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // A malformed header is not worth failing a download over.
    }
  }

  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : fallback;
}

/**
 * Saves `blob` to the browser's downloads.
 *
 * The object URL is released on a timer rather than immediately: revoking it in the same tick
 * races the download the click just started, and the failure is a download that silently never
 * begins.
 */
export function saveFile(blob: Blob, fallbackName?: string) {
  const name = blob instanceof File ? blob.name : fallbackName;
  if (!name) throw new Error("No filename for the download");

  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/**
 * Opens `blob` in a new tab.
 *
 * For the documents that are read or printed rather than filed — a batch of forms going straight
 * to a printer. Anything the desk keeps should go through `saveFile`, because a tab cannot carry
 * the name: whatever the viewer's save button produces is named after the object URL.
 */
export function openFile(blob: Blob) {
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener,noreferrer");
  setTimeout(() => URL.revokeObjectURL(url), 120_000);
}
