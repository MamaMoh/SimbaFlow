import { Check } from "lucide-react";

/**
 * Priced in birr, because the agencies buying this are in Addis and bill in birr.
 *
 * The figures are the old dollar plans converted at roughly 160 and rounded to something a
 * person can say out loud — change the numbers here, they are plain strings.
 */
const PLANS = [
  {
    name: "Starter",
    price: "Br 40,000",
    cadence: "/month",
    blurb: "Smaller agencies, off spreadsheets.",
    features: [
      "Up to 10 staff accounts",
      "The standard pipeline",
      "Unlimited candidates and documents",
      "Generated CVs and contracts",
    ],
  },
  {
    name: "Agency",
    price: "Br 110,000",
    cadence: "/month",
    blurb: "Several destinations and partners at once.",
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
    blurb: "Several agencies, one administration.",
    features: [
      "Multiple agencies, one admin",
      "Single sign-on",
      "API access",
      "Migration of your records",
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
        <h2 className="mkt-display text-center text-[30px] font-semibold text-[var(--mkt-strong)] sm:text-[38px]">
          Priced per agency, not per candidate
        </h2>
        <p className="mt-4 text-center text-[15px] text-[var(--mkt-muted)]">
          Every plan includes your own database, the audit trail and unlimited
          candidate files.
        </p>

        <div className="mt-12 grid items-stretch gap-5 lg:grid-cols-3">
          {PLANS.map((plan) => (
            <div
              key={plan.name}
              className={`flex h-full flex-col rounded-2xl p-7 ${
                plan.featured
                  ? "border border-[var(--mkt-green)]/45 bg-white shadow-[0_20px_50px_-30px_rgba(0,102,51,0.45)]"
                  : "mkt-card"
              }`}
            >
              <h3 className="text-[15px] font-semibold text-[var(--mkt-strong)]">
                {plan.name}
              </h3>
              <p className="mt-1 text-[13px] text-[var(--mkt-muted)]">
                {plan.blurb}
              </p>

              <p className="mt-6 flex items-baseline gap-1">
                <span className="mkt-display text-[32px] font-semibold text-[var(--mkt-strong)]">
                  {plan.price}
                </span>
                <span className="text-[13px] text-[var(--mkt-faint)]">
                  {plan.cadence}
                </span>
              </p>

              <ul className="mt-6 space-y-2.5 border-t border-[var(--mkt-line)] pt-6">
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

              {/* Pushed to the bottom so the three buttons line up whatever the list length. */}
              <a
                href="#contact"
                className={`mt-7 block rounded-full py-2.5 text-center text-[14px] font-semibold transition-opacity hover:opacity-90 ${
                  plan.featured
                    ? "bg-[var(--mkt-strong)] text-white"
                    : "border border-[var(--mkt-line-strong)] text-[var(--mkt-strong)]"
                }`}
                style={{ marginTop: "auto" }}
              >
                Talk to us
              </a>
            </div>
          ))}
        </div>

        <p className="mt-9 text-center text-[12.5px] text-[var(--mkt-faint)]">
          Prices in birr, excluding VAT. A year up front gets two months free.
        </p>
      </div>
    </section>
  );
}
