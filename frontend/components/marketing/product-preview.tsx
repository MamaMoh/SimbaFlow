import Image from "next/image";
import { Bell, Plane } from "lucide-react";

import screen from "@/public/marketing/candidates-screen.webp";

/**
 * The candidates screen, shown in browser chrome.
 *
 * This is a real capture of the running product rather than a drawing of it, so what a visitor
 * sees on the landing page is what they get after signing in. It is taken against seeded sample
 * candidates — every name and passport number in it is invented, because a marketing page is the
 * last place a real candidate's passport number should appear.
 *
 * Re-take it when the screen changes materially:
 *   1600x940 viewport at deviceScaleFactor 2, signed in as an agency owner, /candidates, then
 *   resized to 2400px wide and encoded as WebP (510KB PNG becomes 116KB). Next's image
 *   optimizer is switched off globally in next.config.mjs, so the file is compressed here
 *   rather than on the fly.
 */
export function ProductPreview() {
  return (
    <div className="relative">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -inset-x-8 -top-10 bottom-0 -z-10 blur-3xl"
        style={{
          background:
            "radial-gradient(50% 50% at 30% 30%, rgba(0,102,51,0.16), transparent 70%), radial-gradient(45% 45% at 75% 20%, rgba(248,195,24,0.12), transparent 70%)",
        }}
      />

      <div className="relative overflow-hidden rounded-2xl border border-[var(--mkt-line-strong)] bg-white shadow-[0_30px_80px_-28px_rgba(6,32,21,0.28)]">
        <div className="flex items-center gap-3 border-b border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-4 py-2.5">
          <div className="flex gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-[#ff5f57]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#febc2e]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#28c840]/70" />
          </div>
          <div className="mx-auto hidden items-center gap-2 rounded-md border border-[var(--mkt-line)] bg-white px-3 py-1 text-[11px] text-[var(--mkt-faint)] sm:flex">
            <span className="h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
            app.simbaflow.com/candidates
          </div>
          <div className="ml-auto hidden items-center gap-3 text-[10.5px] text-[var(--mkt-faint)] lg:flex">
            <span className="flex items-center gap-1.5">
              <span className="mkt-pulse h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
              Connected (14ms)
            </span>
            <span className="font-mono">tenant: nile_manpower</span>
          </div>
          <Bell className="ml-auto h-3.5 w-3.5 text-[var(--mkt-faint)] lg:ml-0" />
        </div>

        <Image
          src={screen}
          alt="The SimbaFlow candidates screen: every candidate with their passport, destination, partner agency and current pipeline stage."
          className="block h-auto w-full"
          sizes="(max-width: 1024px) 100vw, 1100px"
          placeholder="blur"
          priority
        />
      </div>

      {/* The one thing a still cannot show: a stage change arriving on its own. */}
      <div className="mkt-float absolute -bottom-5 right-4 hidden items-center gap-3 rounded-xl border border-[var(--mkt-line-strong)] bg-white px-3.5 py-2.5 shadow-[0_16px_40px_-14px_rgba(6,32,21,0.25)] sm:flex">
        <span className="grid h-7 w-7 place-items-center rounded-full bg-[var(--mkt-green)]/15">
          <Plane className="h-3.5 w-3.5 text-[var(--mkt-green)]" />
        </span>
        <div>
          <p className="text-[11.5px] font-medium text-[var(--mkt-strong)]">Abel K. moved to Ticket</p>
          <p className="text-[10.5px] text-[var(--mkt-faint)]">pushed to every desk · just now</p>
        </div>
      </div>
    </div>
  );
}
