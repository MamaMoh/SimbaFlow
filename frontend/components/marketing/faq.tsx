import { Plus } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const QUESTIONS = [
  {
    q: "Can we keep our own stage names and process?",
    a: "Yes — that is the point of the workflow engine. You configure stages, statuses, transition rules and the field conditions that decide which actions appear. The default pipeline is a starting point, not a constraint.",
  },
  {
    q: "How is our data separated from other agencies?",
    a: "Every agency is provisioned its own PostgreSQL schema, and each request is scoped to that schema for its whole lifetime. There is no shared candidate table to filter, so a missed condition cannot expose another agency's data.",
  },
  {
    q: "What happens to the candidates we already have on file?",
    a: "Bring them in during onboarding. Candidate records, passport and labour ID details, and the documents attached to them can be imported, and each one lands at whichever stage it is currently sitting in.",
  },
  {
    q: "Do our branch offices and partner agencies get access?",
    a: "Offices, branches and staff are modelled directly in the system, and you define the roles and permissions that govern what each of them can see or change. Partner agencies live in their own directory with their commission terms attached.",
  },
  {
    q: "Does the team have to refresh to see updates?",
    a: "No. A WebSocket connection pushes stage transitions to every open screen as they happen, with a notification on the change.",
  },
  {
    q: "Can we get the numbers out for regulators and partners?",
    a: "Reporting covers the operational and financial side, and anything you can see on screen exports to Excel or PDF. Every write is also recorded in an audit trail you can hand to an auditor.",
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
              <summary className="flex items-center justify-between gap-6 py-5 text-[15.5px] font-medium text-white transition-colors hover:text-[#7ff0b6]">
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
