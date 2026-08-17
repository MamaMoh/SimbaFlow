import type { ReactNode } from "react";
import "@/components/marketing/marketing.css";

/**
 * Shell for the public marketing site.
 *
 * The root layout locks <body> to the viewport for the application shell, so the
 * marketing pages provide their own scroll container.
 */
export default function MarketingLayout({ children }: { children: ReactNode }) {
  return (
    <div className="mkt mkt-scroll">
      {children}
    </div>
  );
}
