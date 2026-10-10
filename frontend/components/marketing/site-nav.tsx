import Link from "next/link";
import { Logo } from "./logo";

/**
 * The public header.
 *
 * Two links. It carried six — Platform, Workflow engine, Corridors, Security & architecture,
 * Pricing, FAQ — which is a table of contents for a page nobody had agreed to read yet, and the
 * two things a visitor actually came to do (see the price, sign in) were lost among them.
 *
 * No scroll listener and no mobile menu either, so the header ships no JavaScript: it is a
 * hairline and four links at every width.
 */
export function SiteNav() {
  return (
    <header className="sticky top-0 z-50 mkt-glass border-b border-[var(--mkt-line)]">
      <nav className="mx-auto flex h-16 max-w-5xl items-center justify-between gap-4 px-5 sm:px-8">
        <Link href="#top" aria-label="SimbaFlow home">
          <Logo />
        </Link>

        <div className="flex items-center gap-1 sm:gap-2">
          <a
            href="#platform"
            className="hidden rounded-full px-3.5 py-2 text-[13.5px] text-[var(--mkt-muted)] transition-colors hover:text-[var(--mkt-strong)] sm:block"
          >
            Platform
          </a>
          <a
            href="#pricing"
            className="rounded-full px-3.5 py-2 text-[13.5px] text-[var(--mkt-muted)] transition-colors hover:text-[var(--mkt-strong)]"
          >
            Pricing
          </a>
          <Link
            href="/login"
            className="rounded-full px-3.5 py-2 text-[13.5px] font-medium text-[var(--mkt-strong)] transition-colors hover:bg-[var(--mkt-surface)]"
          >
            Sign in
          </Link>
          <a
            href="#demo"
            className="rounded-full bg-[var(--mkt-strong)] px-4 py-2 text-[13.5px] font-semibold text-white transition-opacity hover:opacity-90"
          >
            Book a demo
          </a>
        </div>
      </nav>
    </header>
  );
}
