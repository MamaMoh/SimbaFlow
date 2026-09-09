import Link from "next/link";
import { ArrowRight, CalendarDays, Mail } from "lucide-react";
import { Reveal } from "./reveal";

// The closing band is the one solid-colour block on the page — it has to read as the end of the
// argument, not as another section.
export function Cta() {
  return (
    <section
      id="demo"
      className="relative overflow-hidden bg-[var(--mkt-green-deep)] text-white"
    >
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(60%_60%_at_50%_0%,rgba(255,255,255,0.16),transparent_70%)]"
      />

      <div className="relative mx-auto max-w-3xl px-5 py-24 text-center sm:px-8 sm:py-32">
        <Reveal>
          <h2 className="mkt-display text-[34px] font-semibold text-white sm:text-[52px]">
            See it on <span className="text-[#8ff0bd]">your own agency pipeline</span>
          </h2>
          <p className="mx-auto mt-5 max-w-xl text-[16px] leading-relaxed text-white/75">
            Walk us through how your agency deploys today. We will map it onto SimbaFlow live — stages,
            roles and all — in about thirty minutes.
          </p>
        </Reveal>

        <Reveal delay={80}>
          <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <a
              href="mailto:hello@simbaflow.com?subject=SimbaFlow%20demo"
              className="group inline-flex w-full items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-[15px] font-semibold text-[var(--mkt-green-deep)] transition-transform hover:scale-[1.02] sm:w-auto"
            >
              <CalendarDays className="h-4 w-4" />
              Book a demo
              <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
            </a>
            <Link
              href="/login"
              className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-white/35 px-6 py-3 text-[15px] font-medium text-white transition-colors hover:bg-white/10 sm:w-auto"
            >
              Sign in
            </Link>
          </div>
        </Reveal>

        <Reveal delay={140}>
          <p className="mt-6 inline-flex items-center gap-2 text-[13px] text-white/70">
            <Mail className="h-3.5 w-3.5" />
            Or write to hello@simbaflow.com
          </p>
        </Reveal>
      </div>
    </section>
  );
}
