import { Globe } from "lucide-react";

/**
 * SimbaFlow wordmark for the marketing site.
 *
 * Matches what the app wears: the same globe, and the same Ethiopian flag gradient across the
 * word — green, yellow, red. The site previously used its own mark and a grey "Flow", so the
 * product a visitor met after clicking "Sign in" was branded differently to the page that sold it.
 */
export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <LogoMark className="h-8 w-8" />
      <span className="mkt-wordmark text-[19px] font-bold tracking-[-0.02em]">SimbaFlow</span>
    </span>
  );
}

export function LogoMark({ className = "" }: { className?: string }) {
  return <Globe className={`text-[var(--mkt-green)] ${className}`} aria-hidden="true" />;
}
