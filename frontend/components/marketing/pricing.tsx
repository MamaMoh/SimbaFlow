import { Check, Sparkles } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const PLANS = [
  {
    name: "Starter",
    price: "$249",
    cadence: "/month",
    blurb: "For smaller agencies stepping away from WhatsApp threads and spreadsheets.",
    cta: "Start trial",
    featured: false,
    features: [
      "Up to 10 staff accounts",
      "Default 7-stage deployment pipeline",
      "Unlimited candidate registrations",
      "Document vault & CV export",
      "Standard email support",
    ],
  },
  {
    name: "Agency",
    price: "$690",
    cadence: "/month",
    blurb: "For growing labour exporters managing multi-country desks and partner agencies.",
    cta: "Book a demo",
    featured: true,
    features: [
      "Unlimited staff accounts",
      "Fully configurable stages & transition rules",
      "Parallel tracks (Medical & Embassy)",
      "Commission & double-entry ledger",
      "Partner agency directory portal",
      "One-click Excel & PDF ministry reporting",
      "Priority phone & WhatsApp desk support",
    ],
  },
  {
    name: "Enterprise",
    price: "Custom",
    cadence: "",
    blurb: "For large recruitment consortia, federation bodies and multi-branch groups.",
    cta: "Contact sales",
    featured: false,
    features: [
      "Multiple agency schemas under one master admin",
      "Single sign-on (SAML / Okta / Azure AD)",
      "Custom Telegram & WhatsApp bot notifications",
      "Full REST & GraphQL API access",
      "Dedicated deployment engineer & data migration",
    ],
  },
];

export function Pricing() {
  return (
    <section
      id="pricing"
      className="relative border-y border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-5 py-24 sm:px-8 sm:py-32"
    >
      <div className="mx-auto max-w-6xl">
        <Reveal>
          <SectionHeading
            eyebrow="Fair enterprise pricing"
            title="Priced per agency, not per candidate"
            description="Your busy recruitment cycles shouldn't cost more to operate. Every plan includes the isolated PostgreSQL schema, the audit trail and unlimited candidate files."
          />
        </Reveal>

        <div className="mt-14 grid items-start gap-5 lg:grid-cols-3">
          {PLANS.map((plan, index) => (
            <Reveal key={plan.name} delay={index * 80}>
              <div
                className={`relative h-full rounded-2xl p-7 ${
                  plan.featured
                    ? "border border-[var(--mkt-green)]/45 bg-white shadow-[0_24px_60px_-30px_rgba(15,157,88,0.45)]"
                    : "mkt-card"
                }`}
              >
                {plan.featured && (
                  <span className="absolute -top-3 left-7 inline-flex items-center gap-1.5 rounded-full bg-[var(--mkt-green)] px-3 py-1 text-[11px] font-semibold text-white">
                    <Sparkles className="h-3 w-3" />
                    Most chosen by agencies
                  </span>
                )}

                <h3 className="text-[15px] font-semibold text-[var(--mkt-strong)]">{plan.name}</h3>
                <p className="mt-1.5 text-[13.5px] leading-relaxed text-[var(--mkt-muted)]">{plan.blurb}</p>

                <p className="mt-6 flex items-baseline gap-1">
                  <span className="mkt-display text-[38px] font-semibold text-[var(--mkt-strong)]">{plan.price}</span>
                  <span className="text-[13.5px] text-[var(--mkt-faint)]">{plan.cadence}</span>
                </p>

                <a
                  href="#demo"
                  className={`mt-6 block rounded-full py-2.5 text-center text-[14px] font-semibold transition-transform hover:scale-[1.02] ${
                    plan.featured
                      ? "bg-[var(--mkt-strong)] text-white"
                      : "border border-[var(--mkt-line-strong)] text-[var(--mkt-strong)] hover:bg-[var(--mkt-surface)]"
                  }`}
                >
                  {plan.cta}
                </a>

                <ul className="mt-7 space-y-3 border-t border-[var(--mkt-line)] pt-6">
                  {plan.features.map((feature) => (
                    <li key={feature} className="flex gap-2.5 text-[13.5px] text-[var(--mkt-muted)]">
                      <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--mkt-green)]" />
                      {feature}
                    </li>
                  ))}
                </ul>
              </div>
            </Reveal>
          ))}
        </div>

        <Reveal>
          <p className="mt-10 text-center text-[12.5px] text-[var(--mkt-faint)]">
            All prices in USD. Annual upfront billing receives two months complimentary.
          </p>
        </Reveal>
      </div>
    </section>
  );
}
