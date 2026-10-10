import Link from "next/link";
import { Logo } from "./logo";

/**
 * One line.
 *
 * It had twenty links in four columns — Platform, Workflow, Security, Company — and nineteen of
 * them were anchors back up the same page, three of them to sections that no longer exist. A
 * footer on a one-page site is a signature, not a site map.
 */
export function SiteFooter() {
  return (
    <footer className="border-t border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)]">
      <div className="mx-auto flex max-w-5xl flex-col items-center gap-4 px-5 py-10 sm:flex-row sm:justify-between sm:px-8">
        <Logo />
        <div className="flex items-center gap-6 text-[12.5px] text-[var(--mkt-faint)]">
          <a
            href="#pricing"
            className="transition-colors hover:text-[var(--mkt-strong)]"
          >
            Pricing
          </a>
          <a
            href="#contact"
            className="transition-colors hover:text-[var(--mkt-strong)]"
          >
            Contact
          </a>
          <Link
            href="/login"
            className="transition-colors hover:text-[var(--mkt-strong)]"
          >
            Sign in
          </Link>
        </div>
        <p className="text-[12.5px] text-[var(--mkt-faint)]">
          © {new Date().getFullYear()} SimbaFlow
        </p>
      </div>
    </footer>
  );
}
