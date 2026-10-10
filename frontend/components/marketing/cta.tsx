import Link from "next/link";

/**
 * The closing band, and the only solid-colour block on the page.
 *
 * It used to offer a demo booking. There is nothing behind that button — no calendar, nobody to
 * route it to — so it asked for an email instead of pretending, which is also the one thing the
 * page can actually deliver.
 */
export function Cta() {
  return (
    <section id="contact" className="bg-[var(--mkt-green-deep)] text-white">
      <div className="mx-auto max-w-2xl px-5 py-20 text-center sm:px-8 sm:py-24">
        <h2 className="mkt-display text-[30px] font-semibold text-white sm:text-[40px]">
          See it on your own pipeline
        </h2>
        <p className="mx-auto mt-4 max-w-sm text-[15px] text-white/75">
          Tell us how your agency works today and we will show you the rest.
        </p>

        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <a
            href="mailto:hello@simbaflow.com?subject=SimbaFlow"
            className="inline-flex w-full items-center justify-center rounded-full bg-white px-6 py-3 text-[15px] font-semibold text-[var(--mkt-green-deep)] transition-opacity hover:opacity-90 sm:w-auto"
          >
            hello@simbaflow.com
          </a>
          <Link
            href="/login"
            className="inline-flex w-full items-center justify-center rounded-full border border-white/35 px-6 py-3 text-[15px] font-medium text-white transition-colors hover:bg-white/10 sm:w-auto"
          >
            Sign in
          </Link>
        </div>
      </div>
    </section>
  );
}
