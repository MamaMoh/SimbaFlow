const CORRIDORS = [
  "Saudi Arabia",
  "United Arab Emirates",
  "Qatar",
  "Kuwait",
  "Jordan",
  "Bahrain",
  "Oman",
  "Lebanon",
];

/** Scrolling band of destination corridors, for visual rhythm between sections. */
export function Ticker() {
  return (
    <section className="border-y border-[var(--mkt-line)] bg-[#080f0c] py-8">
      <p className="text-center text-[11.5px] font-semibold uppercase tracking-[0.2em] text-[var(--mkt-faint)]">
        Built for the corridors you deploy into
      </p>

      <div className="mkt-marquee mkt-fade-x mt-6 overflow-hidden">
        <div className="mkt-marquee-track">
          {[0, 1].map((copy) => (
            <ul key={copy} className="flex shrink-0 items-center" aria-hidden={copy === 1}>
              {CORRIDORS.map((corridor) => (
                <li
                  key={corridor}
                  className="flex items-center gap-8 whitespace-nowrap px-8 text-[19px] font-medium tracking-[-0.01em] text-[var(--mkt-muted)]"
                >
                  {corridor}
                  <span className="h-1 w-1 rounded-full bg-[var(--mkt-line-strong)]" />
                </li>
              ))}
            </ul>
          ))}
        </div>
      </div>
    </section>
  );
}
