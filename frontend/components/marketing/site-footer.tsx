import Link from "next/link";
import { Logo } from "./logo";

const COLUMNS = [
  {
    title: "Product",
    links: [
      { label: "Platform", href: "#platform" },
      { label: "Workflow engine", href: "#workflow" },
      { label: "Security", href: "#security" },
      { label: "Pricing", href: "#pricing" },
    ],
  },
  {
    title: "Company",
    links: [
      { label: "Book a demo", href: "#demo" },
      { label: "Contact", href: "mailto:hello@simbaflow.com" },
      { label: "FAQ", href: "#faq" },
    ],
  },
  {
    title: "Account",
    links: [
      { label: "Sign in", href: "/login" },
      { label: "Reset password", href: "/forgot-password" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-[var(--mkt-line)] bg-[#060b09]">
      <div className="mx-auto max-w-6xl px-5 py-14 sm:px-8">
        <div className="grid gap-10 md:grid-cols-[1.4fr_repeat(3,1fr)]">
          <div>
            <Logo />
            <p className="mt-4 max-w-xs text-[13.5px] leading-relaxed text-[var(--mkt-faint)]">
              The operating system for labour export agencies — from candidate intake to commission
              settlement.
            </p>
          </div>

          {COLUMNS.map((column) => (
            <div key={column.title}>
              <p className="text-[12px] font-semibold uppercase tracking-[0.14em] text-white">
                {column.title}
              </p>
              <ul className="mt-4 space-y-2.5">
                {column.links.map((link) => (
                  <li key={link.label}>
                    <Link
                      href={link.href}
                      className="text-[13.5px] text-[var(--mkt-faint)] transition-colors hover:text-white"
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
            <Link href="#" className="transition-colors hover:text-white">
              Privacy
            </Link>
            <Link href="#" className="transition-colors hover:text-white">
              Terms
            </Link>
            <span className="inline-flex items-center gap-1.5">
              <span className="mkt-pulse h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
              All systems operational
            </span>
          </div>
        </div>
      </div>
    </footer>
  );
}
