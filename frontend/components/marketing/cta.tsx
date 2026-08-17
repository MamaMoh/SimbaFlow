import Link from "next/link";
import { ArrowRight, CalendarDays, Mail } from "lucide-react";
import { Reveal } from "./reveal";

export function Cta() {
  return (
    <section id="demo" className="relative overflow-hidden border-t border-[var(--mkt-line)]">
      <div aria-hidden="true" className="mkt-aurora pointer-events-none absolute inset-0 opacity-80" />
      <div aria-hidden="true" className="mkt-grid-lines pointer-events-none absolute inset-0" />

      <div className="relative mx-auto max-w-3xl px-5 py-24 text-center sm:px-8 sm:py-32">
        <Reveal>
          <h2 className="mkt-display text-[34px] font-semibold text-white sm:text-[52px]">
            See it on <span className="mkt-gradient-text">your own pipeline</span>
          </h2>
          <p className="mx-auto mt-5 max-w-xl text-[16px] leading-relaxed text-[var(--mkt-muted)]">
            Walk us through how your agency deploys today. We will map it onto SimbaFlow live — stages,
            roles and all — in about thirty minutes.
          </p>
        </Reveal>

        <Reveal delay={80}>
          <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <a
              href="mailto:hello@simbaflow.com?subject=SimbaFlow%20demo"
              className="group inline-flex w-full items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-[15px] font-semibold text-[#06110b] transition-transform hover:scale-[1.02] sm:w-auto"
            >
              <CalendarDays className="h-4 w-4" />
              Book a demo
              <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
            </a>
            <Link
              href="/login"
              className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-[var(--mkt-line-strong)] px-6 py-3 text-[15px] font-medium text-white transition-colors hover:bg-white/5 sm:w-auto"
            >
              Sign in
            </Link>
          </div>
        </Reveal>

        <Reveal delay={140}>
          <p className="mt-6 inline-flex items-center gap-2 text-[13px] text-[var(--mkt-faint)]">
            <Mail className="h-3.5 w-3.5" />
            Or write to hello@simbaflow.com
          </p>
        </Reveal>
      </div>
    </section>
  );
}
