import {
  Building2,
  FileStack,
  GitBranch,
  Radio,
  ScrollText,
  Wallet,
} from "lucide-react";

/**
 * What the product does, in six lines.
 *
 * Each of these used to be a paragraph with three chips under it, and the section that followed
 * walked the same pipeline a second time stage by stage. A visitor deciding whether to book a
 * demo needs to recognise their own work here, not read the manual — so one line each, and the
 * detail belongs in the demo.
 */
const FEATURES = [
  {
    icon: GitBranch,
    title: "Your own stages",
    body: "Define the pipeline your agency actually runs, with the rules that decide when a candidate may move on.",
  },
  {
    icon: FileStack,
    title: "Candidates and documents",
    body: "Passport, labour ID, medical and contract registered once and carried through every stage, with CVs generated to the format your partner wants.",
  },
  {
    icon: Building2,
    title: "Staff and partners",
    body: "Desk staff, branches and overseas partner agencies in one directory, each seeing only the stages they work on.",
  },
  {
    icon: Radio,
    title: "Live on every desk",
    body: "A stamp, a medical result or a ticket shows up on everyone's screen as it happens. Nobody refreshes and nobody asks.",
  },
  {
    icon: Wallet,
    title: "Commission and expenses",
    body: "Deployment revenue, partner splits and candidate costs kept against the candidate's own file.",
  },
  {
    icon: ScrollText,
    title: "Reports and audit",
    body: "Every action recorded with a name and a time, and ministry reporting exported to Excel or PDF.",
  },
];

export function Features() {
  return (
    <section
      id="platform"
      className="mx-auto max-w-5xl px-5 py-20 sm:px-8 sm:py-24"
    >
      <h2 className="mkt-display mx-auto max-w-2xl text-center text-[30px] font-semibold text-[var(--mkt-strong)] sm:text-[40px]">
        Everything the deployment desk touches
      </h2>

      <div className="mt-12 grid gap-x-10 gap-y-9 sm:grid-cols-2 lg:grid-cols-3">
        {FEATURES.map((feature) => (
          <div key={feature.title}>
            <feature.icon className="h-5 w-5 text-[var(--mkt-green)]" />
            <h3 className="mt-3.5 text-[16px] font-semibold text-[var(--mkt-strong)]">
              {feature.title}
            </h3>
            <p className="mt-1.5 text-[14px] leading-relaxed text-[var(--mkt-muted)]">
              {feature.body}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}
