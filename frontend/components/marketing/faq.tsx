import { Plus } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const QUESTIONS = [
  {
    q: "Can we customise stage names to match our agency process?",
    a: "Yes, completely. SimbaFlow is a workflow engine: you configure stages, transition rules and the conditions that let a button become active. The default seven stages are a starting point you can change at any time.",
  },
  {
    q: "How is our candidate data protected from other agencies?",
    a: "Every agency gets a dedicated PostgreSQL schema. We do not use shared tables with a soft filter — queries are hard-scoped to your namespace, which makes leakage between agencies structurally impossible.",
  },
  {
    q: "Can we import the candidates we already have on spreadsheets?",
    a: "Yes. During onboarding we batch import your existing candidates, passport files and current milestone statuses, and each candidate lands directly in their active pipeline stage.",
  },
  {
    q: "Do our overseas partner agencies get their own access?",
    a: "Yes. Partners are invited into a portal with restricted visibility: they see only the candidates assigned to them, track visa status in real time and download the deployment documents they need.",
  },
  {
    q: "Does the team have to refresh the browser for status changes?",
    a: "No. SimbaFlow holds an open WebSocket connection. When the embassy desk records a visa stamp, the ticketing and departure desks see it immediately.",
  },
];

export function Faq() {
  return (
    <section id="faq" className="mx-auto max-w-3xl px-5 py-24 sm:px-8 sm:py-32">
      <Reveal>
        <SectionHeading eyebrow="FAQ" title="Questions agencies ask first" />
      </Reveal>

      <div className="mkt-faq mt-12 divide-y divide-[var(--mkt-line)] border-y border-[var(--mkt-line)]">
        {QUESTIONS.map((item, index) => (
          <Reveal key={item.q} delay={index * 50}>
            <details className="group">
              <summary className="flex items-center justify-between gap-6 py-5 text-[15.5px] font-medium text-[var(--mkt-strong)] transition-colors hover:text-[var(--mkt-green)]">
                {item.q}
                <Plus className="mkt-faq-icon h-4 w-4 shrink-0 text-[var(--mkt-faint)] transition-transform duration-200" />
              </summary>
              <p className="pb-6 pr-10 text-[14px] leading-relaxed text-[var(--mkt-muted)]">{item.a}</p>
            </details>
          </Reveal>
        ))}
      </div>
    </section>
  );
}
