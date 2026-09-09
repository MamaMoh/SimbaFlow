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
    body: "Define your own stages, transition criteria and action rules. Action buttons appear only when the requirements are satisfied, with parallel tracks for Medical and Tasheer.",
    points: ["Event-sourced history", "Custom per-agency stage gates", "Parallel document tracks"],
  },
  {
    icon: FileStack,
    title: "Candidates and documents",
    body: "Register once: passport details, labour ID, GAMCA medicals and contracts carried through every stage — with generated CV formats and MRZ OCR scanning.",
    points: ["Passport & labour ID vault", "Automated document expiry alerts", "Formatted candidate profiles"],
  },
  {
    icon: Building2,
    title: "Agency ERP",
    body: "Desk staff, branch managers and overseas recruitment partners in one directory, with precise role-based permissions your agency defines.",
    points: ["Staff directory & task assignment", "Partner agencies portal", "Granular stage-by-stage permissions"],
  },
  {
    icon: Radio,
    title: "Real-time everywhere",
    body: "WebSocket feeds update every desk the second a candidate is stamped, passes medical or is ticketed. No manual refreshing.",
    points: ["Sub-second status broadcasts", "In-app notifications & badges", "Always-accurate flight manifests"],
  },
  {
    icon: Wallet,
    title: "Commission and finance",
    body: "Track deployment revenue, partner commission splits and candidate expenses with double-entry reconciliation, linked back to the candidate file.",
    points: ["Double-entry agency ledger", "Split fee calculations", "Settlement & invoice tracking"],
  },
  {
    icon: ScrollText,
    title: "Reporting and audit",
    body: "Every action is written to an immutable audit trail, and ministry and regulator reports export in one click to Excel or PDF.",
    points: ["Complete who/what/when trail", "One-click Excel & PDF export", "Corridor bottleneck metrics"],
  },
];

export function Features() {
  return (
    <section id="platform" className="relative mx-auto max-w-6xl px-5 py-24 sm:px-8 sm:py-32">
      <Reveal>
        <SectionHeading
          eyebrow="The platform"
          title="Everything the deployment desk touches"
          description="Agencies juggle spreadsheets, WhatsApp groups and paper folders. SimbaFlow organises the entire overseas pipeline into one coordinated system without breaking your operating rhythm."
        />
      </Reveal>

      <div className="mt-14 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {FEATURES.map((feature, index) => (
          <Reveal key={feature.title} delay={index * 60}>
            <div className="mkt-card group h-full p-6">
              <span className="inline-grid h-10 w-10 place-items-center rounded-xl border border-[var(--mkt-line)] bg-[var(--mkt-green)]/10 text-[var(--mkt-green)] transition-colors group-hover:bg-[var(--mkt-green)]/20">
                <feature.icon className="h-[18px] w-[18px]" />
              </span>
              <h3 className="mt-5 text-[17px] font-semibold text-[var(--mkt-strong)]">{feature.title}</h3>
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
