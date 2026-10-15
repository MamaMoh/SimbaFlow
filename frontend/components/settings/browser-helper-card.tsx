"use client";

import { useEffect, useState } from "react";
import { CheckCircle2, Download, ExternalLink } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PASTE_HELPER_VERSION, atLeast } from "@/lib/site-bridge/copy-to-form";

/**
 * Where people get the browser helper.
 *
 * Two routes, and which one is shown depends on whether the extension has been published yet.
 * Once it is on the Chrome Web Store, NEXT_PUBLIC_EXTENSION_STORE_URL points at the listing and
 * installing is one click with updates arriving on their own. Until then the same screen hands
 * over the folder and the three steps for loading it by hand, which is the only way to try it
 * before it is listed.
 *
 * It stops offering either once the extension is installed. It knows because the extension
 * leaves a mark on this page — the alternative is the page knowing the extension's id, which is
 * not stable for one loaded from a folder, so it would have been wrong exactly when it mattered.
 */
const STORE_URL = process.env.NEXT_PUBLIC_EXTENSION_STORE_URL ?? "";

export function BrowserHelperCard() {
  const [version, setVersion] = useState<string | null>(null);

  useEffect(() => {
    const read = () => document.documentElement.dataset.simbaflowHelper ?? null;
    setVersion(read());

    // The mark is set before this app mounts, so it is normally found first time. Looking again
    // for a few seconds is for the person who installs it with this screen already open: they
    // should see it take effect rather than be left wondering whether to reload.
    const timer = setInterval(() => {
      const found = read();
      if (found) {
        setVersion(found);
        clearInterval(timer);
      }
    }, 1000);
    const stop = setTimeout(() => clearInterval(timer), 15000);

    return () => {
      clearInterval(timer);
      clearTimeout(stop);
    };
  }, []);

  return (
    <section className="space-y-4 rounded-lg border bg-card p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold">Browser helper</h2>
        <p className="text-muted-foreground mt-0.5 text-xs">
          Fills a candidate into the insurance and visa portals and the LMIS contract form,
          instead of typing every box for each person. Chrome and Edge.
        </p>
      </div>

      {version ? (
        <div className="space-y-2">
          <p className="flex items-center gap-2 text-sm font-medium text-emerald-700">
            <CheckCircle2 className="size-4 shrink-0" />
            Installed — version {version}
          </p>
          <p className="text-muted-foreground text-xs">
            Open the insurance or visa form, click the SimbaFlow button in the toolbar, and search
            for the candidate. It fills the boxes; you check them and press the form&rsquo;s own
            button.
          </p>
          {atLeast(version, PASTE_HELPER_VERSION) ? (
            <p className="text-muted-foreground text-xs">
              Or right-click a candidate on the Candidates or LMIS board, choose{" "}
              <strong>Copy to insurance</strong> or <strong>Copy to LMIS contract</strong>, and paste
              into that form. The first time, open the helper on the form and turn on{" "}
              <strong>Fill when I paste here</strong>.
            </p>
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-amber-700">
                Filling by pasting needs version {PASTE_HELPER_VERSION}. Download it again, replace
                the folder, and press the reload arrow on{" "}
                <code className="bg-muted rounded px-1 py-0.5">chrome://extensions</code>.
              </p>
              <Button asChild variant="outline" size="sm" className="gap-1.5">
                <a href="/simbaflow-form-filler.zip" download>
                  <Download className="size-4" />
                  Download {PASTE_HELPER_VERSION}
                </a>
              </Button>
            </div>
          )}
        </div>
      ) : STORE_URL ? (
        <div className="space-y-2">
          <Button asChild className="gap-1.5">
            <a href={STORE_URL} target="_blank" rel="noreferrer noopener">
              <ExternalLink className="size-4" />
              Add to Chrome
            </a>
          </Button>
          <p className="text-muted-foreground text-xs">
            Sign in here in the same browser and it will find your candidates on its own — there
            is nothing to set up.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          <Button asChild variant="outline" className="gap-1.5">
            <a href="/simbaflow-form-filler.zip" download>
              <Download className="size-4" />
              Download the helper
            </a>
          </Button>

          <ol className="text-muted-foreground list-decimal space-y-1 pl-5 text-xs">
            <li>Unzip it somewhere it can stay — deleting the folder removes the extension.</li>
            <li>
              Open <code className="bg-muted rounded px-1 py-0.5">chrome://extensions</code> and
              turn on <strong>Developer mode</strong>.
            </li>
            <li>
              Press <strong>Load unpacked</strong> and choose the unzipped folder.
            </li>
          </ol>

          <p className="text-muted-foreground text-xs">
            Not yet on the Chrome Web Store, so it installs by hand for now.
          </p>
        </div>
      )}

      <p className="text-muted-foreground text-xs">
        It fills forms and never submits them, and it reads nothing except what you ask it to
        fill.{" "}
        <a className="underline" href="/privacy" target="_blank" rel="noreferrer">
          What it can see
        </a>
        .
      </p>
    </section>
  );
}
