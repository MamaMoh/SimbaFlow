import {
  Banknote,
  Bell,
  Briefcase,
  CalendarClock,
  ClipboardList,
  Download,
  FileText,
  LayoutGrid,
  MapPin,
  MoreHorizontal,
  Plane,
  Search,
  SlidersHorizontal,
  Ticket,
  TriangleAlert,
  Users,
} from "lucide-react";

/**
 * Stylised rendering of the candidates screen.
 *
 * Static markup rather than a screenshot: it stays sharp at any size, keeps pace with the product
 * without re-shooting images, and the passport numbers are invented rather than real people's.
 */

const NAV = [
  { icon: LayoutGrid, label: "Dashboard" },
  { icon: Users, label: "Candidates", active: true },
  { icon: ClipboardList, label: "My Work" },
];

const PIPELINE = [
  { icon: FileText, label: "New Contracts" },
  { icon: Briefcase, label: "Embassy" },
  { icon: ClipboardList, label: "Case Executive" },
  { icon: FileText, label: "LMIS" },
  { icon: Ticket, label: "Tickets" },
  { icon: CalendarClock, label: "Departures" },
  { icon: MapPin, label: "Arrivals" },
  { icon: TriangleAlert, label: "Exceptions" },
  { icon: Banknote, label: "Commissions" },
];

const ROWS = [
  { n: 1, name: "REGATU HASHU MEGERSO", date: "8/26/2026", passport: "EP8919142", age: 25, worksIn: "Saudi Arabia", partner: "Etenaa Resources", stage: "Embassy", tone: "amber" },
  { n: 2, name: "MEYREMA AHMED MUHAMED", date: "8/18/2026", passport: "E00409142", age: 27, worksIn: "Kuwait", partner: "Nile Manpower", stage: "LMIS", tone: "sky" },
  { n: 3, name: "ASTER DABA LEMA", date: "8/14/2026", passport: "EQ2648388", age: 24, worksIn: "Qatar", partner: "Horn Recruit", stage: "Embassy", tone: "amber" },
  { n: 4, name: "MEDINA ABDU YASIN", date: "8/09/2026", passport: "E00639490", age: 31, worksIn: "UAE", partner: "Etenaa Resources", stage: "Ticket", tone: "green" },
  { n: 5, name: "ADANECH DINKU HUNDE", date: "8/02/2026", passport: "EQ2013249", age: 22, worksIn: "Saudi Arabia", partner: "Nile Manpower", stage: "Intake", tone: "slate" },
];

const TONES: Record<string, string> = {
  green: "border-[var(--mkt-green)]/30 bg-[var(--mkt-green)]/10 text-[var(--mkt-green)]",
  amber: "border-[var(--mkt-amber-ink)]/30 bg-[var(--mkt-yellow)]/20 text-[var(--mkt-amber-ink)]",
  sky: "border-sky-500/30 bg-sky-500/10 text-sky-700",
  slate: "border-[var(--mkt-line-strong)] bg-[var(--mkt-surface)] text-[var(--mkt-muted)]",
};

export function ProductPreview() {
  return (
    <div className="relative">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -inset-x-8 -top-10 bottom-0 -z-10 blur-3xl"
        style={{
          background:
            "radial-gradient(50% 50% at 30% 30%, rgba(15,157,88,0.16), transparent 70%), radial-gradient(45% 45% at 75% 20%, rgba(168,127,22,0.1), transparent 70%)",
        }}
      />

      <div className="relative overflow-hidden rounded-2xl border border-[var(--mkt-line-strong)] bg-white shadow-[0_30px_80px_-28px_rgba(6,32,21,0.28)]">
        {/* Window chrome */}
        <div className="flex items-center gap-3 border-b border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] px-4 py-2.5">
          <div className="flex gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-[#ff5f57]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#febc2e]/70" />
            <span className="h-2.5 w-2.5 rounded-full bg-[#28c840]/70" />
          </div>
          <div className="mx-auto hidden items-center gap-2 rounded-md border border-[var(--mkt-line)] bg-white px-3 py-1 text-[11px] text-[var(--mkt-faint)] sm:flex">
            <span className="h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
            app.simbaflow.com/candidates
          </div>
          <div className="ml-auto hidden items-center gap-3 text-[10.5px] text-[var(--mkt-faint)] lg:flex">
            <span className="flex items-center gap-1.5">
              <span className="mkt-pulse h-1.5 w-1.5 rounded-full bg-[var(--mkt-green)]" />
              Connected (14ms)
            </span>
            <span className="font-mono">tenant: nile_manpower</span>
          </div>
          <Bell className="ml-auto h-3.5 w-3.5 text-[var(--mkt-faint)] lg:ml-0" />
        </div>

        <div className="flex">
          {/* Sidebar */}
          <aside className="hidden w-[186px] shrink-0 flex-col gap-1 border-r border-[var(--mkt-line)] bg-[var(--mkt-bg-raised)] p-3 md:flex">
            <div className="mb-3 flex items-center gap-2 rounded-lg border border-[var(--mkt-line)] bg-white px-2.5 py-1.5 text-[11px] text-[var(--mkt-faint)]">
              <Search className="h-3 w-3" />
              Search
            </div>
            {NAV.map((item) => (
              <span
                key={item.label}
                className={`flex items-center gap-2 rounded-lg px-2.5 py-1.5 text-[11.5px] ${
                  item.active
                    ? "bg-[var(--mkt-green)]/12 font-medium text-[var(--mkt-strong)] ring-1 ring-inset ring-[var(--mkt-green)]/30"
                    : "text-[var(--mkt-muted)]"
                }`}
              >
                <item.icon className="h-3.5 w-3.5" />
                {item.label}
              </span>
            ))}

            <p className="mt-4 px-2.5 text-[9.5px] font-semibold uppercase tracking-[0.14em] text-[var(--mkt-faint)]">
              Workflow pipeline
            </p>
            {PIPELINE.map((item) => (
              <span
                key={item.label}
                className="flex items-center gap-2 rounded-lg px-2.5 py-1.5 text-[11.5px] text-[var(--mkt-muted)]"
              >
                <item.icon className="h-3.5 w-3.5" />
                {item.label}
              </span>
            ))}

            <div className="mt-4 border-t border-[var(--mkt-line)] pt-3">
              <p className="text-[11px] font-medium text-[var(--mkt-strong)]">System Administrator</p>
              <p className="text-[10px] text-[var(--mkt-faint)]">admin@simbaflow.local</p>
            </div>
          </aside>

          {/* Candidates screen */}
          <div className="min-w-0 flex-1 p-4 sm:p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-[13px] font-semibold text-[var(--mkt-strong)] sm:text-[15px]">Candidates</p>
                <p className="mt-0.5 text-[11px] text-[var(--mkt-faint)]">
                  Manage candidate registrations and track pipeline progress
                </p>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="inline-flex items-center gap-1.5 rounded-md border border-[var(--mkt-line)] px-2 py-1 text-[10.5px] text-[var(--mkt-muted)]">
                  <SlidersHorizontal className="h-3 w-3" /> View
                </span>
                <span className="inline-flex items-center gap-1.5 rounded-md border border-[var(--mkt-line)] px-2 py-1 text-[10.5px] text-[var(--mkt-muted)]">
                  <Download className="h-3 w-3" /> Export
                </span>
                <span className="rounded-md bg-[var(--mkt-green-deep)] px-2.5 py-1 text-[10.5px] font-medium text-white">
                  + Create
                </span>
              </div>
            </div>

            <div className="mt-3 flex items-center gap-1 text-[10.5px]">
              {["Active", "Inactive", "All"].map((tab, i) => (
                <span
                  key={tab}
                  className={`rounded-md px-2 py-1 ${
                    i === 0
                      ? "bg-[var(--mkt-green)]/12 font-medium text-[var(--mkt-green-deep)]"
                      : "text-[var(--mkt-faint)]"
                  }`}
                >
                  {tab}
                </span>
              ))}
            </div>

            <div className="mt-3 overflow-hidden rounded-xl border border-[var(--mkt-line)]">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="bg-[var(--mkt-bg-raised)] text-[9.5px] uppercase tracking-[0.08em] text-[var(--mkt-faint)]">
                    <th className="px-2.5 py-2 font-semibold">#</th>
                    <th className="px-2.5 py-2 font-semibold">Name</th>
                    <th className="hidden px-2.5 py-2 font-semibold lg:table-cell">Passport</th>
                    <th className="hidden px-2.5 py-2 font-semibold xl:table-cell">Age</th>
                    <th className="hidden px-2.5 py-2 font-semibold xl:table-cell">Works in</th>
                    <th className="hidden px-2.5 py-2 font-semibold lg:table-cell">Partner</th>
                    <th className="px-2.5 py-2 font-semibold">Stage</th>
                    <th className="px-2.5 py-2 text-center font-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {ROWS.map((row) => (
                    <tr key={row.n} className="border-t border-[var(--mkt-line)] text-[11px]">
                      <td className="px-2.5 py-2 text-[var(--mkt-faint)]">{row.n}</td>
                      <td className="max-w-[180px] truncate px-2.5 py-2 font-medium text-[var(--mkt-strong)]">
                        {row.name}
                      </td>
                      <td className="hidden px-2.5 py-2 font-mono text-[10.5px] text-[var(--mkt-muted)] lg:table-cell">
                        {row.passport}
                      </td>
                      <td className="hidden px-2.5 py-2 text-[var(--mkt-muted)] xl:table-cell">{row.age}</td>
                      <td className="hidden px-2.5 py-2 text-[var(--mkt-muted)] xl:table-cell">{row.worksIn}</td>
                      <td className="hidden max-w-[130px] truncate px-2.5 py-2 text-[var(--mkt-muted)] lg:table-cell">
                        {row.partner}
                      </td>
                      <td className="px-2.5 py-2">
                        <span
                          className={`inline-flex rounded-full border px-2 py-0.5 text-[10px] ${TONES[row.tone]}`}
                        >
                          {row.stage}
                        </span>
                      </td>
                      <td className="px-2.5 py-2 text-center text-[var(--mkt-faint)]">
                        <MoreHorizontal className="mx-auto h-3.5 w-3.5" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-3 flex items-center justify-between text-[10px] text-[var(--mkt-faint)]">
              <span>Page 1 of 1</span>
              <span className="flex items-center gap-2">
                <span>Rows per page 10</span>
                <span className="rounded border border-[var(--mkt-line)] px-1.5 py-0.5">First</span>
                <span className="rounded border border-[var(--mkt-line)] px-1.5 py-0.5">Next</span>
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Live-update badge, the one thing a static screenshot cannot show. */}
      <div className="mkt-float absolute -bottom-5 right-4 hidden items-center gap-3 rounded-xl border border-[var(--mkt-line-strong)] bg-white px-3.5 py-2.5 shadow-[0_16px_40px_-14px_rgba(6,32,21,0.25)] sm:flex">
        <span className="grid h-7 w-7 place-items-center rounded-full bg-[var(--mkt-green)]/15">
          <Plane className="h-3.5 w-3.5 text-[var(--mkt-green)]" />
        </span>
        <div>
          <p className="text-[11.5px] font-medium text-[var(--mkt-strong)]">Abel K. moved to Ticket</p>
          <p className="text-[10.5px] text-[var(--mkt-faint)]">pushed to every desk · just now</p>
        </div>
      </div>
    </div>
  );
}
