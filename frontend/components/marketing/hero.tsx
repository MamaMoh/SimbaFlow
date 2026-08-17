import Link from "next/link";
import { ArrowRight, PlayCircle, ShieldCheck } from "lucide-react";
import { ProductPreview } from "./product-preview";
import { Reveal } from "./reveal";

const PROOF = [
  { value: "One board", label: "every candidate, every stage" },
  { value: "Schema-per-tenant", label: "hard data isolation" },
  { value: "Real-time", label: "status the moment it changes" },
  { value: "Full audit trail", label: "who did what, when" },
];

export function Hero() {
  return (
    <section id="top" className="relative overflow-hidden">
      <div aria-hidden="true" className="mkt-aurora pointer-events-none absolute inset-0" />
      <div aria-hidden="true" className="mkt-grid-lines pointer-events-none absolute inset-0" />

      <div className="relative mx-auto max-w-6xl px-5 pb-20 pt-16 sm:px-8 sm:pb-28 sm:pt-24">
        <Reveal className="flex justify-center">
          <a
            href="#workflow"
            className="group inline-flex items-center gap-2 rounded-full border border-[var(--mkt-line)] bg-white/[0.04] py-1.5 pl-1.5 pr-3.5 text-[12.5px] text-[var(--mkt-muted)] transition-colors hover:border-[var(--mkt-green)]/40 hover:text-white"
          >
            <span className="rounded-full bg-[var(--mkt-green)]/15 px-2 py-0.5 text-[11px] font-semibold text-[#7ff0b6]">
              New
            </span>
            Configurable workflow engine, per agency
            <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
          </a>
        </Reveal>

        <Reveal delay={60}>
          <h1 className="mkt-display mx-auto mt-7 max-w-4xl text-center text-[42px] font-semibold sm:text-[62px] lg:text-[74px]">
            Run overseas deployment
            <br className="hidden sm:block" />{" "}
            <span className="mkt-gradient-text">without losing the thread</span>
          </h1>
        </Reveal>

        <Reveal delay={120}>
          <p className="mx-auto mt-6 max-w-2xl text-center text-[16.5px] leading-relaxed text-[var(--mkt-muted)] sm:text-[18px]">
            SimbaFlow is the operating system for labour export agencies — candidate intake, embassy
            and Tasheer processing, government clearances, travel logistics and commission settlement,
            all on one pipeline your whole team can see.
          </p>
        </Reveal>

        <Reveal delay={180}>
          <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <a
              href="#demo"
              className="group inline-flex w-full items-center justify-center gap-2 rounded-full bg-white px-6 py-3 text-[15px] font-semibold text-[#06110b] transition-transform hover:scale-[1.02] sm:w-auto"
            >
              Book a demo
              <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
            </a>
            <Link
              href="/login"
              className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-[var(--mkt-line-strong)] px-6 py-3 text-[15px] font-medium text-white transition-colors hover:bg-white/5 sm:w-auto"
            >
              <PlayCircle className="h-4 w-4" />
              Sign in to your agency
            </Link>
          </div>
        </Reveal>

        <Reveal delay={240}>
          <p className="mt-5 flex items-center justify-center gap-2 text-center text-[12.5px] text-[var(--mkt-faint)]">
            <ShieldCheck className="h-3.5 w-3.5" />
            Isolated database schema per agency · MFA on every account
          </p>
        </Reveal>

        <Reveal delay={140} className="mt-16 sm:mt-20">
          <ProductPreview />
        </Reveal>

        <Reveal delay={80}>
          <dl className="mt-24 grid grid-cols-2 gap-x-6 gap-y-8 border-t border-[var(--mkt-line)] pt-10 lg:grid-cols-4">
            {PROOF.map((item) => (
              <div key={item.value}>
                <dt className="text-[17px] font-semibold text-white sm:text-[19px]">{item.value}</dt>
                <dd className="mt-1 text-[13px] leading-snug text-[var(--mkt-faint)]">{item.label}</dd>
              </div>
            ))}
          </dl>
        </Reveal>
      </div>
    </section>
  );
}
