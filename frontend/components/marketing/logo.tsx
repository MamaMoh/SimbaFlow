/**
 * SimbaFlow wordmark for the marketing site.
 *
 * The mark is a candidate moving through the pipeline: a dot entering, a curve,
 * and an arrow leaving — drawn over the brand gradient used across the app.
 */
export function Logo({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <LogoMark className="h-8 w-8" />
      <span className="text-[17px] font-semibold tracking-[-0.02em] text-white">
        Simba<span className="text-[var(--mkt-muted)] font-medium">Flow</span>
      </span>
    </span>
  );
}

export function LogoMark({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" fill="none" className={className} aria-hidden="true">
      <defs>
        <linearGradient id="mkt-logo-g" x1="0" y1="0" x2="32" y2="32" gradientUnits="userSpaceOnUse">
          <stop stopColor="#00A862" />
          <stop offset="0.55" stopColor="#0E7A3C" />
          <stop offset="1" stopColor="#F8C318" />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="9" fill="url(#mkt-logo-g)" />
      <rect width="32" height="32" rx="9" fill="none" stroke="rgba(255,255,255,0.22)" />
      <path
        d="M8.5 21.5c4.4 0 3.6-5.4 7.5-5.4s3.1-5.6 7.5-5.6"
        stroke="white"
        strokeWidth="2.1"
        strokeLinecap="round"
        fill="none"
      />
      <circle cx="8.5" cy="21.5" r="2.1" fill="white" />
      <path d="M20.9 8.1 24.4 10.5 21.1 13.2" stroke="white" strokeWidth="2.1" strokeLinecap="round" strokeLinejoin="round" fill="none" />
    </svg>
  );
}
