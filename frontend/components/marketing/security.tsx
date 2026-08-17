import { Check, Database, Fingerprint, KeyRound, Lock } from "lucide-react";
import { Reveal } from "./reveal";
import { SectionHeading } from "./section-heading";

const GUARANTEES = [
  {
    icon: Database,
    title: "Schema-per-tenant isolation",
    body: "Each agency gets its own PostgreSQL schema. Not a tenant column you have to trust — a separate namespace, so one agency's query cannot reach another's rows.",
  },
  {
    icon: KeyRound,
    title: "JWT with refresh tokens",
    body: "Short-lived access tokens, rotating refresh tokens and server-side revocation on logout.",
  },
  {
    icon: Fingerprint,
    title: "MFA and granular roles",
    body: "Multi-factor authentication on any account, with permissions your agency defines down to the individual action.",
  },
  {
    icon: Lock,
    title: "Audit trail on every write",
    body: "Who changed what, when, and from where — retained for the whole life of the candidate record.",
  },
];

const TENANTS = [
  { name: "nile_manpower", rows: "12,480 records", tint: "#12b76a" },
  { name: "horn_recruit", rows: "8,109 records", tint: "#f8c318" },
  { name: "abyss_overseas", rows: "3,922 records", tint: "#38bdf8" },
];

export function Security() {
  return (
    <section id="security" className="mx-auto max-w-6xl px-5 py-24 sm:px-8 sm:py-32">
      <div className="grid items-center gap-14 lg:grid-cols-2 lg:gap-20">
        <div>
          <Reveal>
            <SectionHeading
              align="left"
              eyebrow="Security"
              title="Multi-tenant, and it means it"
              description="You are holding passports, medical records and contracts for people who cannot afford a data leak. Isolation is structural here, not a filter someone might forget to apply."
            />
          </Reveal>

          <div className="mt-10 space-y-7">
            {GUARANTEES.map((item, index) => (
              <Reveal key={item.title} delay={index * 70}>
                <div className="flex gap-4">
                  <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-[var(--mkt-line)] bg-white/[0.04] text-[var(--mkt-green)]">
                    <item.icon className="h-4 w-4" />
                  </span>
                  <div>
                    <h3 className="text-[15.5px] font-semibold text-white">{item.title}</h3>
                    <p className="mt-1.5 text-[14px] leading-relaxed text-[var(--mkt-muted)]">{item.body}</p>
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        </div>

        <Reveal delay={100}>
          <div className="relative">
            <div
              aria-hidden="true"
              className="pointer-events-none absolute -inset-8 opacity-70 blur-3xl"
              style={{
                background:
                  "radial-gradient(50% 50% at 60% 40%, rgba(18,183,106,0.2), transparent 70%)",
              }}
            />
            <div className="relative rounded-2xl border border-[var(--mkt-line-strong)] bg-[#080f0c] p-6">
              <div className="flex items-center justify-between">
                <p className="font-mono text-[11.5px] text-[var(--mkt-faint)]">simbaflow · postgres 16</p>
                <span className="rounded-full border border-[var(--mkt-line)] px-2 py-0.5 font-mono text-[10.5px] text-[var(--mkt-faint)]">
                  isolated
                </span>
              </div>

              <div className="mt-5 space-y-3">
                {TENANTS.map((tenant) => (
                  <div
                    key={tenant.name}
                    className="rounded-xl border border-[var(--mkt-line)] bg-white/[0.02] p-4"
                  >
                    <div className="flex items-center gap-2.5">
                      <span className="h-2 w-2 rounded-full" style={{ background: tenant.tint }} />
                      <span className="font-mono text-[12.5px] text-white">{tenant.name}</span>
                      <span className="ml-auto font-mono text-[11px] text-[var(--mkt-faint)]">
                        {tenant.rows}
                      </span>
                    </div>
                    <div className="mt-3 grid grid-cols-6 gap-1">
                      {Array.from({ length: 6 }).map((_, cell) => (
                        <span
                          key={cell}
                          className="h-1.5 rounded-full"
                          style={{
                            background: tenant.tint,
                            opacity: 0.5 - cell * 0.06,
                          }}
                        />
                      ))}
                    </div>
                  </div>
                ))}
              </div>

              <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-[var(--mkt-green)]/25 bg-[var(--mkt-green)]/8 p-3.5">
                <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--mkt-green)]" />
                <p className="text-[12.5px] leading-relaxed text-[#a9e8c6]">
                  Cross-tenant reads are impossible by construction — the connection is scoped to a
                  single schema for the life of the request.
                </p>
              </div>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
