import { Check } from "lucide-react";

/**
 * Priced in birr, because the agencies buying this are in Addis and bill in birr.
 *
 * The figures are the previous dollar plans converted at roughly 160 birr and rounded to
 * something a person can say out loud — change the numbers here, they are plain strings.
 *
 * Four lines per plan rather than seven: a feature list long enough to need reading is a list
 * nobody compares, and it was also where the page promised things in passing.
 */
const PLANS = [
  {
    name: "Starter",
    price: "Br 40,000",
    cadence: "/month",
    blurb: "For smaller agencies coming off spreadsheets and WhatsApp groups.",
    cta: "Book a demo",
    featured: false,
    features: [
      "Up to 10 staff accounts",
      "The standard deployment pipeline",
      "Unlimited candidates and documents",
      "Generated CVs and contracts",
    ],
  },
  {
    name: "Agency",
    price: "Br 110,000",
    cadence: "/month",
    blurb:
      "For agencies running several destinations and partner agencies at once.",
    cta: "Book a demo",
    featured: true,
    features: [
      "Unlimited staff accounts",
      "Your own stages and rules",
      "Commission and expense ledger",
      "Partner agency directory",
    ],
  },
  {
    name: "Enterprise",
    price: "Custom",
    cadence: "",
    blurb: "For groups running several agencies under one administration.",
    cta: "Talk to us",
    featured: false,
    features: [
      "Several agencies, one administration",
      "Single sign-on",
      "API access",
      "Migration of your existing records",
    ],
  },
];

export function Pricing() {
  return (
    <section
      id="pricing"
      className="border-y border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-5 py-20 sm:px-8 sm:py-24"
    >
      <div className="mx-auto max-w-5xl">
        <h2 className="mkt-display text-center text-[30px] font-semibold text-[var(--mkt-strong)] sm:text-[40px]">
          Priced per agency, not per candidate
        </h2>
        <p className="mx-auto mt-4 max-w-lg text-center text-[15.5px] leading-relaxed text-[var(--mkt-muted)]">
          A busy season should not cost more to run. Every plan includes your
          own isolated database, the audit trail and unlimited candidate files.
        </p>

        <div className="mt-12 grid items-start gap-5 lg:grid-cols-3">
          {PLANS.map((plan) => (
            <div
              key={plan.name}
              className={`relative h-full rounded-2xl p-7 ${
                plan.featured
                  ? "border border-[var(--mkt-green)]/45 bg-white shadow-[0_20px_50px_-30px_rgba(0,102,51,0.45)]"
                  : "mkt-card"
              }`}
            >
              <h3 className="text-[15px] font-semibold text-[var(--mkt-strong)]">
                {plan.name}
              </h3>
              <p className="mt-1.5 text-[13.5px] leading-relaxed text-[var(--mkt-muted)]">
                {plan.blurb}
              </p>

              <p className="mt-6 flex items-baseline gap-1">
                <span className="mkt-display text-[34px] font-semibold text-[var(--mkt-strong)]">
                  {plan.price}
                </span>
                <span className="text-[13.5px] text-[var(--mkt-faint)]">
                  {plan.cadence}
                </span>
              </p>

              <a
                href="#demo"
                className={`mt-6 block rounded-full py-2.5 text-center text-[14px] font-semibold transition-opacity hover:opacity-90 ${
                  plan.featured
                    ? "bg-[var(--mkt-strong)] text-white"
                    : "border border-[var(--mkt-line-strong)] text-[var(--mkt-strong)]"
                }`}
              >
                {plan.cta}
              </a>

              <ul className="mt-7 space-y-3 border-t border-[var(--mkt-line)] pt-6">
                {plan.features.map((feature) => (
                  <li
                    key={feature}
                    className="flex gap-2.5 text-[13.5px] text-[var(--mkt-muted)]"
                  >
                    <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--mkt-green)]" />
                    {feature}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <p className="mt-9 text-center text-[12.5px] text-[var(--mkt-faint)]">
          Prices in Ethiopian birr, excluding VAT. Paying a year ahead gets two
          months free.
        </p>
      </div>
    </section>
  );
}
