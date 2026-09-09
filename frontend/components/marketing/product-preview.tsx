import { Bell, CheckCircle2, Clock3, FileText, Plane, Search, Users } from "lucide-react";

/**
 * Stylised rendering of the candidate pipeline board.
 *
 * Static markup rather than a screenshot so it stays sharp at any size and
 * keeps pace with the product without re-shooting images.
 */

const COLUMNS = [
  {
    stage: "Intake",
    count: 24,
    tint: "#12b76a",
    cards: [
      { name: "Hanan A.", meta: "EP52••••1", tag: "Docs verified", done: true },
      { name: "Yonas T.", meta: "EP41••••8", tag: "Medical booked" },
    ],
  },
  {
    stage: "Embassy",
    count: 11,
    tint: "#d99e00",
    cards: [
      { name: "Meseret G.", meta: "Riyadh · KSA", tag: "Tasheer pending" },
      { name: "Abel K.", meta: "Doha · QAT", tag: "Visa stamped", done: true },
    ],
  },
  {
    stage: "LMIS",
    count: 9,
    tint: "#38bdf8",
    cards: [{ name: "Sara M.", meta: "MoLS · Batch 42", tag: "Awaiting clearance" }],
  },
  {
    stage: "Departure",
    count: 6,
    tint: "#c30f16",
    cards: [
      { name: "Dawit L.", meta: "ET 445 · 06:20", tag: "In 3 days" },
      { name: "Lidya B.", meta: "ET 612 · 21:45", tag: "In 6 days" },
    ],
  },
];

const SIDEBAR = [
  { icon: Users, label: "Candidates", active: true },
  { icon: FileText, label: "Documents" },
  { icon: Plane, label: "Travel" },
  { icon: Clock3, label: "Timeline" },
];

export function ProductPreview() {
  return (
    <div className="relative">
      {/* Glow bed under the window */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -inset-x-10 -top-6 bottom-10 rounded-[40px] opacity-70 blur-3xl"
        style={{
          background:
            "radial-gradient(50% 50% at 30% 30%, rgba(18,183,106,0.28), transparent 70%), radial-gradient(45% 45% at 75% 20%, rgba(248,195,24,0.16), transparent 70%)",
        }}
      />

      <div className="relative overflow-hidden rounded-2xl border border-[var(--mkt-line-strong)] bg-[var(--mkt-bg-raised)] shadow-[0_30px_80px_-28px_rgba(6,32,21,0.28)]">
        {/* Window chrome */}
        <div className="flex items-center gap-3 border-b border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-4 py-2.5">
          <div className="flex gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-[#ff5f57]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#febc2e]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#28c840]/70" />
          </div>
          <div className="mx-auto hidden items-center gap-2 rounded-md border border-[var(--mkt-line)] bg-[var(--mkt-surface-strong)] px-3 py-1 text-[11px] text-[var(--mkt-faint)] sm:flex">
            <span className="h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
            app.simbaflow.com/candidates
          </div>
          <Bell className="ml-auto h-3.5 w-3.5 text-[var(--mkt-faint)] sm:ml-0" />
        </div>

        <div className="flex">
          {/* Sidebar */}
          <aside className="hidden w-[176px] shrink-0 flex-col gap-1 border-r border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] p-3 md:flex">
            <div className="mb-3 flex items-center gap-2 rounded-lg border border-[var(--mkt-line)] bg-[var(--mkt-surface-strong)] px-2.5 py-1.5 text-[11px] text-[var(--mkt-faint)]">
              <Search className="h-3 w-3" />
              Search
            </div>
            {SIDEBAR.map((item) => (
              <div
                key={item.label}
                className={`flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-[12.5px] ${
                  item.active
                    ? "bg-[var(--mkt-green)]/12 text-[var(--mkt-strong)] ring-1 ring-inset ring-[var(--mkt-green)]/30"
                    : "text-[var(--mkt-faint)]"
                }`}
              >
                <item.icon className="h-3.5 w-3.5" />
                {item.label}
              </div>
            ))}
            <div className="mt-auto rounded-lg border border-[var(--mkt-line)] p-2.5">
              <p className="text-[10px] uppercase tracking-widest text-[var(--mkt-faint)]">Agency</p>
              <p className="mt-1 text-[12px] text-[var(--mkt-strong)]">Nile Manpower</p>
            </div>
          </aside>

          {/* Board */}
          <div className="min-w-0 flex-1 p-3.5 sm:p-5">
            <div className="mb-4 flex items-end justify-between gap-4">
              <div>
                <p className="text-[13px] font-semibold text-[var(--mkt-strong)] sm:text-[15px]">Candidate pipeline</p>
                <p className="mt-0.5 text-[11px] text-[var(--mkt-faint)]">50 active · updated just now</p>
              </div>
              <div className="hidden items-center gap-1.5 rounded-full border border-[var(--mkt-green)]/30 bg-[var(--mkt-green)]/10 px-2.5 py-1 text-[10.5px] text-[var(--mkt-green)] sm:flex">
                <span className="mkt-pulse h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
                Live
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2.5 lg:grid-cols-4">
              {COLUMNS.map((column, index) => (
                <div
                  key={column.stage}
                  className={`rounded-xl border border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] p-2.5 ${
                    index > 1 ? "hidden lg:block" : ""
                  }`}
                >
                  <div className="mb-2.5 flex items-center gap-1.5">
                    <span className="h-1.5 w-1.5 rounded-full" style={{ background: column.tint }} />
                    <span className="text-[11.5px] font-medium text-[var(--mkt-strong)]">{column.stage}</span>
                    <span className="ml-auto text-[10.5px] text-[var(--mkt-faint)]">{column.count}</span>
                  </div>

                  <div className="space-y-2">
                    {column.cards.map((card) => (
                      <div
                        key={card.name}
                        className="rounded-lg border border-[var(--mkt-line)] bg-white p-2.5"
                      >
                        <div className="flex items-center gap-2">
                          <span
                            className="grid h-5 w-5 place-items-center rounded-full text-[9px] font-semibold text-white"
                            style={{ background: column.tint }}
                          >
                            {card.name.charAt(0)}
                          </span>
                          <span className="truncate text-[11.5px] text-[var(--mkt-strong)]">{card.name}</span>
                        </div>
                        <p className="mt-1.5 truncate text-[10px] text-[var(--mkt-faint)]">{card.meta}</p>
                        <div className="mt-2 flex items-center gap-1 text-[10px]">
                          {card.done ? (
                            <CheckCircle2 className="h-3 w-3 text-[var(--mkt-green)]" />
                          ) : (
                            <Clock3 className="h-3 w-3 text-[var(--mkt-yellow)]" />
                          )}
                          <span className={card.done ? "text-[var(--mkt-green)]" : "text-[#a97a00]"}>{card.tag}</span>
                        </div>
                      </div>
                    ))}

                    <div className="rounded-lg border border-dashed border-[var(--mkt-line)] px-2.5 py-2 text-center text-[10px] text-[var(--mkt-faint)]">
                      + {column.count - column.cards.length} more
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Realtime toast, floating over the window */}
      <div className="mkt-float absolute -bottom-5 right-4 hidden items-center gap-3 rounded-xl border border-[var(--mkt-line-strong)] bg-white px-3.5 py-2.5 shadow-[0_16px_40px_-14px_rgba(6,32,21,0.25)] sm:flex">
        <span className="grid h-7 w-7 place-items-center rounded-full bg-[var(--mkt-green)]/15">
          <CheckCircle2 className="h-3.5 w-3.5 text-[var(--mkt-green)]" />
        </span>
        <div className="text-left">
          <p className="text-[11.5px] font-medium text-[var(--mkt-strong)]">Abel K. moved to Ticket</p>
          <p className="text-[10px] text-[var(--mkt-faint)]">Visa stamped · by Selam H.</p>
        </div>
      </div>
    </div>
  );
}
