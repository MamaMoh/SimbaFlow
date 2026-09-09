import { Globe } from "lucide-react";

/**
 * SimbaFlow wordmark for the marketing site.
 *
 * The mark is the same globe the app signs in under and carries in its sidebar, in the same brand
 * green. The marketing site previously used a mark of its own, so the product a visitor saw after
 * clicking "Sign in" wore a different badge to the one that sold it.
 */
export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <LogoMark className="h-8 w-8" />
      <span className="text-[17px] font-semibold tracking-[-0.02em] text-[var(--mkt-strong)]">
        Simba<span className="text-[var(--mkt-muted)] font-medium">Flow</span>
      </span>
    </span>
  );
}

export function LogoMark({ className = "" }: { className?: string }) {
  return <Globe className={`text-[var(--mkt-green)] ${className}`} aria-hidden="true" />;
}
