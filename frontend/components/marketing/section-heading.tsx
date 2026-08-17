import type { ReactNode } from "react";

export function SectionHeading({
  eyebrow,
  title,
  description,
  align = "center",
}: {
  eyebrow: string;
  title: ReactNode;
  description?: string;
  align?: "center" | "left";
}) {
  const centered = align === "center";

  return (
    <div className={centered ? "mx-auto max-w-2xl text-center" : "max-w-2xl"}>
      <p className="text-[12px] font-semibold uppercase tracking-[0.18em] text-[var(--mkt-green)]">
        {eyebrow}
      </p>
      <h2 className="mkt-display mt-3.5 text-[32px] font-semibold text-white sm:text-[44px]">{title}</h2>
      {description && (
        <p className="mt-4 text-[16px] leading-relaxed text-[var(--mkt-muted)]">{description}</p>
      )}
    </div>
  );
}
