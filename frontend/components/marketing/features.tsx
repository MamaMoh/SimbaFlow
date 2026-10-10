import {
  Building2,
  FileStack,
  GitBranch,
  Radio,
  ScrollText,
  Wallet,
} from "lucide-react";

/**
 * What the product does, in a line each.
 *
 * These were paragraphs, and before that paragraphs with three chips under them. Six paragraphs
 * is a page nobody finishes; six lines is something you can take in at a glance and recognise
 * your own work in, which is all this section has to do.
 */
const FEATURES = [
  {
    icon: GitBranch,
    title: "Your own stages",
    body: "The pipeline your agency runs, with the rules for moving on.",
  },
  {
    icon: FileStack,
    title: "Candidates and documents",
    body: "Registered once, carried through every stage, printed in your partner's format.",
  },
  {
    icon: Building2,
    title: "Staff and partners",
    body: "One directory, each person seeing only the stages they work on.",
  },
  {
    icon: Radio,
    title: "Live on every desk",
    body: "A stamp or a ticket shows on everyone's screen as it happens.",
  },
  {
    icon: Wallet,
    title: "Commission and expenses",
    body: "Revenue, partner splits and costs kept on the candidate's file.",
  },
  {
    icon: ScrollText,
    title: "Reports and audit",
    body: "Every action named and timed; ministry reports in one click.",
  },
];

export function Features() {
  return (
    <section
      id="platform"
      className="mx-auto max-w-5xl px-5 py-20 sm:px-8 sm:py-24"
    >
      <h2 className="mkt-display text-center text-[30px] font-semibold text-[var(--mkt-strong)] sm:text-[38px]">
        Everything the deployment desk touches
      </h2>

      <div className="mt-12 grid gap-x-10 gap-y-9 sm:grid-cols-2 lg:grid-cols-3">
        {FEATURES.map((feature) => (
          <div key={feature.title}>
            <feature.icon className="h-5 w-5 text-[var(--mkt-green)]" />
            <h3 className="mt-3.5 text-[15.5px] font-semibold text-[var(--mkt-strong)]">
              {feature.title}
            </h3>
            <p className="mt-1.5 text-[13.5px] leading-relaxed text-[var(--mkt-muted)]">
              {feature.body}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}
