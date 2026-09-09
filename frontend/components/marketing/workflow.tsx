import { GitFork, Layers, SlidersHorizontal } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const STAGES = [
  {
    name: "Intake",
    milestone: "KYC & bio verification",
    body: "Register the candidate, scan the passport with OCR, verify biographical data.",
  },
  {
    name: "Embassy",
    milestone: "Visa file & Enjaz sync",
    body: "Enjaz slip creation, Tasheer appointments, stamping and collection.",
  },
  {
    name: "LMIS",
    milestone: "Ministry registry batch",
    body: "Government labour ministry clearance, quota verification, batch clearances.",
  },
  {
    name: "Ticket",
    milestone: "PNR & travel schedule",
    body: "Flight booking, PNR validation, itinerary generation and sponsor notification.",
  },
  {
    name: "Departure",
    milestone: "Pre-flight handover",
    body: "Airport desk check-in, passenger briefing, embarkation confirmation.",
  },
  {
    name: "Arrival",
    milestone: "Sponsor sign-off",
    body: "Employer reception confirmation, iqama issuance check, follow-up.",
  },
  {
    name: "Commission",
    milestone: "Ledger reconciled",
    body: "Invoice sponsor and partner, split desk earnings, double-entry settlement.",
  },
];

const TRAITS = [
  {
    icon: SlidersHorizontal,
    title: "Stages you control",
    body: "Rename them, reorder them, add your own. Nothing here is hard-coded into the product.",
  },
  {
    icon: GitFork,
    title: "Parallel tracks",
    body: "Medical and Tasheer can run at the same time without one blocking the other.",
  },
  {
    icon: Layers,
    title: "Event-sourced",
    body: "Every transition is an event, so the timeline reconstructs exactly what happened.",
  },
];

export function Workflow() {
  return (
    <section id="workflow" className="relative border-y border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)]">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 opacity-60"
        style={{
          background:
            "radial-gradient(60% 40% at 50% 0%, rgba(18,183,106,0.12), transparent 70%)",
        }}
      />

      <div className="relative mx-auto max-w-6xl px-5 py-24 sm:px-8 sm:py-32">
        <Reveal>
          <SectionHeading
            eyebrow="Workflow"
            title="Your pipeline, not a template"
            description="SimbaFlow ships with the default overseas deployment stages below. Every agency then tailors them to match their operating procedures — rules, milestones and desk assignments."
          />
        </Reveal>

        <Reveal delay={80}>
          <ol className="relative mt-16 flex flex-col gap-8 lg:flex-row lg:gap-4">
            {/* Rail behind the stage markers */}
            <span
              aria-hidden="true"
              className="absolute left-[7px] top-2 bottom-2 w-px bg-gradient-to-b from-[var(--mkt-green)]/60 via-[var(--mkt-line-strong)] to-transparent lg:left-2 lg:right-2 lg:top-[7px] lg:bottom-auto lg:h-px lg:w-auto lg:bg-gradient-to-r"
            />

            {STAGES.map((stage, index) => (
              <li key={stage.name} className="relative flex-1 pl-8 lg:pl-0">
                <span
                  className={`absolute left-0 top-1.5 h-3.5 w-3.5 rounded-full border-2 lg:relative lg:top-0 lg:block ${
                    index === 0
                      ? "border-[var(--mkt-green)] bg-[var(--mkt-green)]"
                      : "border-[var(--mkt-line-strong)] bg-[var(--mkt-bg-raised)]"
                  }`}
                />
                <p className="flex items-baseline gap-2 text-[10.5px] font-semibold uppercase tracking-[0.16em] text-[var(--mkt-faint)] lg:mt-5">
                  <span className="mkt-display text-[15px] tracking-normal text-[var(--mkt-green)]">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                  Stage {index + 1}
                </p>
                <h3 className="mt-1 text-[16px] font-semibold text-[var(--mkt-strong)]">{stage.name}</h3>
                <p className="mt-1.5 text-[13px] leading-relaxed text-[var(--mkt-muted)]">{stage.body}</p>
                {/* What the stage closes out — the words the desk actually uses for "done". */}
                <p className="mt-2 inline-flex rounded-full border border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-2 py-0.5 text-[11px] text-[var(--mkt-muted)]">
                  {stage.milestone}
                </p>
              </li>
            ))}
          </ol>
        </Reveal>

        <div className="mt-16 grid gap-4 md:grid-cols-3">
          {TRAITS.map((trait, index) => (
            <Reveal key={trait.title} delay={index * 70}>
              <div className="mkt-card h-full p-6">
                <trait.icon className="h-[18px] w-[18px] text-[var(--mkt-amber-ink)]" />
                <h3 className="mt-4 text-[15.5px] font-semibold text-[var(--mkt-strong)]">{trait.title}</h3>
                <p className="mt-2 text-[13.5px] leading-relaxed text-[var(--mkt-muted)]">{trait.body}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
