import Link from "next/link";
import { Logo } from "./logo";

/**
 * The public header.
 *
 * Three links and the one button. It carried six — Platform, Workflow engine, Corridors,
 * Security & architecture, Pricing, FAQ — a table of contents for a page nobody had agreed to
 * read yet, and the thing a visitor actually came to do was lost among them.
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
          <a
            href="#contact"
            className="rounded-full px-3.5 py-2 text-[13.5px] text-[var(--mkt-muted)] transition-colors hover:text-[var(--mkt-strong)]"
          >
            Contact
          </a>
          <Link
            href="/login"
            className="rounded-full bg-[var(--mkt-strong)] px-4 py-2 text-[13.5px] font-semibold text-white transition-opacity hover:opacity-90"
          >
            Sign in
          </Link>
        </div>
      </nav>
    </header>
  );
}
