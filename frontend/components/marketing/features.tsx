import {
  Building2,
  FileStack,
  GitBranch,
  Radio,
  ScrollText,
  Wallet,
} from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const FEATURES = [
  {
    icon: GitBranch,
    title: "Configurable workflow engine",
    body: "Define your own stages, statuses and transition rules. Action buttons appear only when the field conditions are met, and parallel tracks like Medical and Tasheer run side by side.",
    points: ["Event-sourced history", "Per-agency stage rules", "Parallel tracks"],
  },
  {
    icon: FileStack,
    title: "Candidates and documents",
    body: "Register a candidate once and carry passport details, labour ID, medicals and contracts through every stage — with a generated CV and a full status timeline attached.",
    points: ["Passport & labour ID", "Document vault", "Auto-generated CV"],
  },
  {
    icon: Building2,
    title: "Agency ERP",
    body: "Staff and partner agencies in one directory, with custom roles and permissions each agency defines for itself.",
    points: ["Staff directory", "Partner directory", "Custom roles"],
  },
  {
    icon: Radio,
    title: "Real-time everywhere",
    body: "A WebSocket connection pushes stage transitions to every open screen, so the visa desk and the travel desk are never working from a stale list.",
    points: ["Live status changes", "Instant notifications", "No refresh needed"],
  },
  {
    icon: Wallet,
    title: "Commission and finance",
    body: "Track what each deployment earns with double-entry accounting, from partner splits through to settlement — tied back to the candidate that generated it.",
    points: ["Double-entry ledger", "Partner splits", "Settlement tracking"],
  },
  {
    icon: ScrollText,
    title: "Reporting and audit",
    body: "Every operation is written to an audit trail, and the numbers your regulators and partners ask for export to Excel or PDF without a spreadsheet detour.",
    points: ["Immutable audit log", "Excel & PDF export", "Operational analytics"],
  },
];

export function Features() {
  return (
    <section id="platform" className="relative mx-auto max-w-6xl px-5 py-24 sm:px-8 sm:py-32">
      <Reveal>
        <SectionHeading
          eyebrow="The platform"
          title="Everything the deployment desk touches"
          description="Agencies run this work across spreadsheets, WhatsApp threads and a filing cabinet. SimbaFlow puts the whole lifecycle in one system without flattening how your agency actually operates."
        />
      </Reveal>

      <div className="mt-14 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {FEATURES.map((feature, index) => (
          <Reveal key={feature.title} delay={index * 60}>
            <div className="mkt-card group h-full p-6">
              <span className="inline-grid h-10 w-10 place-items-center rounded-xl border border-[var(--mkt-line)] bg-[var(--mkt-green)]/10 text-[var(--mkt-green)] transition-colors group-hover:bg-[var(--mkt-green)]/20">
                <feature.icon className="h-[18px] w-[18px]" />
              </span>
              <h3 className="mt-5 text-[17px] font-semibold text-white">{feature.title}</h3>
              <p className="mt-2.5 text-[14px] leading-relaxed text-[var(--mkt-muted)]">{feature.body}</p>
              <ul className="mt-5 flex flex-wrap gap-1.5">
                {feature.points.map((point) => (
                  <li
                    key={point}
                    className="rounded-full border border-[var(--mkt-line)] px-2.5 py-1 text-[11.5px] text-[var(--mkt-faint)]"
                  >
                    {point}
                  </li>
                ))}
              </ul>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}
