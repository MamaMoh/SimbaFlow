import { GitFork, Layers, SlidersHorizontal } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const STAGES = [
  { name: "Intake", body: "Register the candidate, collect documents, verify the passport." },
  { name: "Embassy", body: "Visa file, Tasheer appointment, stamping and collection." },
  { name: "LMIS", body: "Government labour registration and clearance." },
  { name: "Ticket", body: "Booking, fare confirmation and itinerary issue." },
  { name: "Departure", body: "Countdown, briefing and airport handover." },
  { name: "Arrival", body: "Employer confirmation and probation follow-up." },
  { name: "Commission", body: "Invoice, partner split and settlement." },
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
    <section id="workflow" className="relative border-y border-[var(--mkt-line)] bg-[#0a120f]">
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
            description="SimbaFlow ships with the default deployment pipeline below. Every agency then bends it to the way they actually work — stages, statuses, transition rules and the conditions that decide which action buttons appear."
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
                      : "border-[var(--mkt-line-strong)] bg-[#0a120f]"
                  }`}
                />
                <p className="text-[10.5px] font-semibold uppercase tracking-[0.16em] text-[var(--mkt-faint)] lg:mt-5">
                  Stage {index + 1}
                </p>
                <h3 className="mt-1 text-[16px] font-semibold text-white">{stage.name}</h3>
                <p className="mt-1.5 text-[13px] leading-relaxed text-[var(--mkt-muted)]">{stage.body}</p>
              </li>
            ))}
          </ol>
        </Reveal>

        <div className="mt-16 grid gap-4 md:grid-cols-3">
          {TRAITS.map((trait, index) => (
            <Reveal key={trait.title} delay={index * 70}>
              <div className="mkt-card h-full p-6">
                <trait.icon className="h-[18px] w-[18px] text-[var(--mkt-yellow)]" />
                <h3 className="mt-4 text-[15.5px] font-semibold text-white">{trait.title}</h3>
                <p className="mt-2 text-[13.5px] leading-relaxed text-[var(--mkt-muted)]">{trait.body}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
