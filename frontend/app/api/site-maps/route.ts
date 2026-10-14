import { NextResponse } from "next/server";
import { getCachedServerSession } from "@/lib/api/session-cache";
import { SITE_MANIFESTS } from "@/lib/site-bridge/manifest";

/**
 * The field maps, served to whoever is going to use them.
 *
 * This exists so that the browser extension carries none of this knowledge itself. An extension
 * that knew the insurance form by heart would need rebuilding, re-signing and re-installing on
 * every desk the day that form renamed a box — and portals rename boxes without telling anybody.
 * Fetching the maps instead turns that into a deploy here, which is a thing we already do.
 *
 * Behind the session, because the maps describe our own field names and the shape of the records
 * behind them. Not a secret worth much, but not something to hand to anyone who asks either.
 *
 * No CORS headers, deliberately. The extension reads this from its background worker, where a
 * host permission for this origin puts the request outside CORS entirely — and the same request
 * carries the signed-in session cookie, so there is no second credential to issue, store on a
 * laptop and eventually forget to revoke.
 */
export async function GET() {
  const session = await getCachedServerSession();
  if (!session?.user?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  return NextResponse.json(
    { data: SITE_MANIFESTS },
    // Worth a short cache — the extension asks on every page it wakes up on, and these change
    // about as often as the portals do.
    { headers: { "Cache-Control": "private, max-age=300" } },
  );
}
