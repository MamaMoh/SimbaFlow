import Link from "next/link";
import { KeyRound, RefreshCw, ShieldCheck } from "lucide-react";
import { ProductPreview } from "./product-preview";

/** Stated one by one rather than as a single sentence, so each reads as its own guarantee. */
const GUARANTEES = [
  { icon: ShieldCheck, label: "Separate database per agency" },
  { icon: KeyRound, label: "MFA on every account" },
  { icon: RefreshCw, label: "Live updates on every desk" },
];

/**
 * The first screen, and most of what the page has to do.
 *
 * One line under the headline. Everything a landing page wants to add here — the corridors, the
 * stack, the promises — is a sentence the visitor reads instead of looking at the screenshot,
 * which says more than the sentence did.
 */
export function Hero() {
  return (
    <section id="top" className="relative overflow-hidden">
      <div
        aria-hidden="true"
        className="mkt-aurora pointer-events-none absolute inset-0"
      />

      <div className="relative mx-auto max-w-5xl px-5 pb-16 pt-14 sm:px-8 sm:pb-24 sm:pt-20">
        <h1 className="mkt-display mx-auto max-w-3xl text-center text-[40px] font-semibold sm:text-[58px]">
          Run overseas deployment
          <br className="hidden sm:block" />{" "}
          <span className="text-[var(--mkt-green)]">
            without losing the thread
          </span>
        </h1>

        <p className="mx-auto mt-5 max-w-lg text-center text-[16px] leading-relaxed text-[var(--mkt-muted)]">
          One pipeline for a labour export agency, from intake to commission.
        </p>

        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Link
            href="/login"
            className="inline-flex w-full items-center justify-center rounded-full bg-[var(--mkt-strong)] px-6 py-3 text-[15px] font-semibold text-white transition-opacity hover:opacity-90 sm:w-auto"
          >
            Sign in to your agency
          </Link>
          <a
            href="#contact"
            className="inline-flex w-full items-center justify-center rounded-full border border-[var(--mkt-line-strong)] px-6 py-3 text-[15px] font-medium text-[var(--mkt-strong)] transition-colors hover:bg-[var(--mkt-surface)] sm:w-auto"
          >
            Talk to us
          </a>
        </div>

        <ul className="mt-6 flex flex-wrap items-center justify-center gap-x-7 gap-y-3 text-[12.5px] text-[var(--mkt-faint)]">
          {GUARANTEES.map((g) => (
            <li key={g.label} className="flex items-center gap-2">
              <g.icon className="h-3.5 w-3.5 text-[var(--mkt-green)]" />
              {g.label}
            </li>
          ))}
        </ul>

        <div className="mt-14">
          <ProductPreview />
        </div>
      </div>
    </section>
  );
}
