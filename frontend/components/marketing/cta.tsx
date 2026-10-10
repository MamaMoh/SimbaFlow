import Link from "next/link";
import { ArrowRight } from "lucide-react";

/** The closing band, and the only solid-colour block on the page. */
export function Cta() {
  return (
    <section id="demo" className="bg-[var(--mkt-green-deep)] text-white">
      <div className="mx-auto max-w-2xl px-5 py-20 text-center sm:px-8 sm:py-24">
        <h2 className="mkt-display text-[30px] font-semibold text-white sm:text-[42px]">
          See it on your own pipeline
        </h2>
        <p className="mx-auto mt-4 max-w-md text-[15.5px] leading-relaxed text-white/75">
          Show us how your agency deploys today and we will set it up on
          SimbaFlow while you watch — about half an hour.
        </p>

        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <a
            href="mailto:hello@simbaflow.com?subject=SimbaFlow%20demo"
            className="group inline-flex w-full items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-[15px] font-semibold text-[var(--mkt-green-deep)] transition-opacity hover:opacity-90 sm:w-auto"
          >
            Book a demo
            <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
          </a>
          <Link
            href="/login"
            className="inline-flex w-full items-center justify-center rounded-full border border-white/35 px-6 py-3 text-[15px] font-medium text-white transition-colors hover:bg-white/10 sm:w-auto"
          >
            Sign in
          </Link>
        </div>

        <p className="mt-6 text-[13px] text-white/70">
          Or write to hello@simbaflow.com
        </p>
      </div>
    </section>
  );
}
