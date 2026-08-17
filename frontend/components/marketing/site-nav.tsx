"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Menu, X, ArrowRight } from "lucide-react";
import { Logo } from "./logo";

const LINKS = [
  { href: "#platform", label: "Platform" },
  { href: "#workflow", label: "Workflow" },
  { href: "#security", label: "Security" },
  { href: "#pricing", label: "Pricing" },
  { href: "#faq", label: "FAQ" },
];

export function SiteNav() {
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);

  // The page scrolls inside .mkt-scroll, so listen there instead of on window.
  useEffect(() => {
    const scroller = document.querySelector(".mkt-scroll");
    if (!scroller) return;

    const onScroll = () => setScrolled(scroller.scrollTop > 12);
    onScroll();
    scroller.addEventListener("scroll", onScroll, { passive: true });
    return () => scroller.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header className="sticky top-0 z-50">
      <div
        className={`transition-all duration-300 ${
          scrolled ? "mkt-glass border-b border-[var(--mkt-line)]" : "border-b border-transparent"
        }`}
      >
        <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between px-5 sm:px-8">
          <Link href="#top" aria-label="SimbaFlow home">
            <Logo />
          </Link>

          <div className="hidden items-center gap-1 md:flex">
            {LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                className="rounded-full px-3.5 py-2 text-[13.5px] text-[var(--mkt-muted)] transition-colors hover:bg-white/5 hover:text-white"
              >
                {link.label}
              </a>
            ))}
          </div>

          <div className="hidden items-center gap-2.5 md:flex">
            <Link
              href="/login"
              className="rounded-full px-4 py-2 text-[13.5px] font-medium text-[var(--mkt-muted)] transition-colors hover:text-white"
            >
              Sign in
            </Link>
            <a
              href="#demo"
              className="group inline-flex items-center gap-1.5 rounded-full bg-white px-4 py-2 text-[13.5px] font-semibold text-[#06110b] transition-transform hover:scale-[1.03]"
            >
              Book a demo
              <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
            </a>
          </div>

          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-label={open ? "Close menu" : "Open menu"}
            aria-expanded={open}
            className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-[var(--mkt-line)] text-white md:hidden"
          >
            {open ? <X className="h-4.5 w-4.5" /> : <Menu className="h-4.5 w-4.5" />}
          </button>
        </nav>
      </div>

      {open && (
        <div className="mkt-glass border-b border-[var(--mkt-line)] px-5 pb-6 pt-2 md:hidden">
          <div className="flex flex-col">
            {LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={() => setOpen(false)}
                className="border-b border-[var(--mkt-line)] py-3.5 text-[15px] text-[var(--mkt-muted)]"
              >
                {link.label}
              </a>
            ))}
          </div>
          <div className="mt-5 flex flex-col gap-2.5">
            <Link
              href="/login"
              className="rounded-full border border-[var(--mkt-line-strong)] py-2.5 text-center text-sm font-medium text-white"
            >
              Sign in
            </Link>
            <a
              href="#demo"
              onClick={() => setOpen(false)}
              className="rounded-full bg-white py-2.5 text-center text-sm font-semibold text-[#06110b]"
            >
              Book a demo
            </a>
          </div>
        </div>
      )}
    </header>
  );
}
