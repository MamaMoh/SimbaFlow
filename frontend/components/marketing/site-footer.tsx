import Link from "next/link";
import { Logo } from "./logo";

const COLUMNS = [
  {
    title: "Platform",
    links: [
      { label: "Overview", href: "#platform" },
      { label: "Candidate table", href: "#platform" },
      { label: "Document vault", href: "#platform" },
      { label: "Audit trails", href: "#security" },
      { label: "WebSocket engine", href: "#platform" },
    ],
  },
  {
    title: "Workflow",
    links: [
      { label: "Intake & OCR", href: "#workflow" },
      { label: "Embassy & Tasheer", href: "#workflow" },
      { label: "LMIS ministry sync", href: "#workflow" },
      { label: "Ticketing desk", href: "#workflow" },
      { label: "Commission ledger", href: "#workflow" },
    ],
  },
  {
    title: "Security",
    links: [
      { label: "Schema isolation", href: "#security" },
      { label: "MFA & role RBAC", href: "#security" },
      { label: "Data encryption", href: "#security" },
      { label: "Audit logs", href: "#security" },
    ],
  },
  {
    title: "Company",
    links: [
      { label: "Pricing plans", href: "#pricing" },
      { label: "Partner agencies", href: "#platform" },
      { label: "Book a demo", href: "#demo" },
      { label: "Contact desk", href: "mailto:hello@simbaflow.com" },
      { label: "FAQ", href: "#faq" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)]">
      <div className="mx-auto max-w-6xl px-5 py-14 sm:px-8">
        <div className="grid gap-10 md:grid-cols-[1.4fr_repeat(4,1fr)]">
          <div>
            <Logo />
            <p className="mt-4 max-w-xs text-[13.5px] leading-relaxed text-[var(--mkt-faint)]">
              The operating system for labour export agencies — from candidate intake to commission
              settlement.
            </p>
          </div>

          {COLUMNS.map((column) => (
            <div key={column.title}>
              <p className="text-[12px] font-semibold uppercase tracking-[0.14em] text-[var(--mkt-strong)]">
                {column.title}
              </p>
              <ul className="mt-4 space-y-2.5">
                {column.links.map((link) => (
                  <li key={link.label}>
                    <Link
                      href={link.href}
                      className="text-[13.5px] text-[var(--mkt-faint)] transition-colors hover:text-[var(--mkt-strong)]"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-12 flex flex-col gap-3 border-t border-[var(--mkt-line)] pt-6 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-[12.5px] text-[var(--mkt-faint)]">
            © {new Date().getFullYear()} SimbaFlow. All rights reserved.
          </p>
          <div className="flex items-center gap-5 text-[12.5px] text-[var(--mkt-faint)]">
            <Link href="#" className="transition-colors hover:text-[var(--mkt-strong)]">
              Privacy
            </Link>
            <Link href="#" className="transition-colors hover:text-[var(--mkt-strong)]">
              Terms
            </Link>
            <span className="inline-flex items-center gap-1.5">
              <span className="mkt-pulse h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
              All systems operational · PostgreSQL 16
            </span>
          </div>
        </div>
      </div>
    </footer>
  );
}
