import type { Metadata } from "next";
import { Eye, Lock, Send, ShieldCheck, Ban, KeyRound } from "lucide-react";

/**
 * The privacy policy for the browser helper.
 *
 * Public, and it has to be: the Chrome Web Store will not list an extension whose policy sits
 * behind a sign-in, and a policy only the people who already have the thing can read is not a
 * policy. /privacy is in the middleware's public routes for that reason.
 *
 * It carries its own scrolling. The app's body is `h-screen overflow-hidden` because the shell
 * manages its own scroll areas, so a page that simply runs long under the root layout is cut off
 * at the fold with no way down.
 *
 * Everything below describes what the extension in this repository actually does. If the
 * extension changes, this changes with it — a policy that has drifted from the code is worse
 * than none, because it is a promise nobody kept.
 */

/**
 * Where to write about any of this. The Chrome Web Store requires a working address, and a
 * published policy pointing at a mailbox nobody reads is the kind of thing that is discovered
 * at the worst moment. Change this to whichever address is actually watched.
 */
const CONTACT = "support@laba.et";
const UPDATED = "14 October 2026";

export const metadata: Metadata = {
  title: "Privacy — SimbaFlow Form Filler",
  description:
    "What the SimbaFlow Form Filler browser extension can see, what it sends, and what it stores.",
};

function Glance({
  icon: Icon,
  title,
  children,
}: {
  icon: typeof Eye;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-card rounded-lg border p-4">
      <Icon className="text-muted-foreground size-4" />
      <p className="mt-2.5 text-sm font-medium">{title}</p>
      <p className="text-muted-foreground mt-1 text-xs leading-relaxed">{children}</p>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="scroll-mt-20">
      <h2 className="text-base font-semibold tracking-tight">{title}</h2>
      <div className="text-muted-foreground mt-2.5 space-y-3 text-sm leading-relaxed">
        {children}
      </div>
    </section>
  );
}

export default function PrivacyPage() {
  return (
    <div className="bg-background text-foreground h-screen overflow-y-auto">
      <header className="bg-background/85 sticky top-0 z-10 border-b backdrop-blur">
        <div className="mx-auto flex max-w-3xl items-center gap-2.5 px-6 py-3.5">
          <svg
            viewBox="0 0 24 24"
            className="size-5 shrink-0"
            fill="none"
            stroke="#006633"
            strokeWidth="2.1"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="9.6" />
            <path d="M12 2.4a14.5 14.5 0 0 0 0 19.2 14.5 14.5 0 0 0 0-19.2" />
            <path d="M2.4 12h19.2" />
          </svg>
          <span className="font-semibold tracking-tight">SimbaFlow</span>
          <span className="text-muted-foreground text-sm">Form Filler</span>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 pt-10 pb-20">
        <div className="space-y-3">
          <span className="text-muted-foreground bg-muted inline-flex rounded-full px-2.5 py-1 text-xs font-medium">
            Updated {UPDATED}
          </span>
          <h1 className="text-2xl font-semibold tracking-tight">Privacy</h1>
          <p className="text-muted-foreground max-w-xl text-sm leading-relaxed">
            The SimbaFlow Form Filler is a browser extension for agency staff. It types a
            candidate&rsquo;s details — the ones the agency already holds — into the insurance and
            visa portals, so nobody copies sixteen boxes by hand for each person.
          </p>
        </div>

        <div className="mt-8 grid gap-3 sm:grid-cols-3">
          <Glance icon={Eye} title="What it sees">
            The page you are on, only at the moment you click its button. Nothing else, and no
            other tab.
          </Glance>
          <Glance icon={Send} title="What it sends">
            Nothing to anyone but your own SimbaFlow. No analytics, no third parties, nothing
            sold.
          </Glance>
          <Glance icon={Lock} title="What it stores">
            One setting, if you change it. No candidate data, no password, no history.
          </Glance>
        </div>

        <div className="mt-12 space-y-10">
          <Section title="What it can see">
            <p>
              <strong className="text-foreground">The page you are on, only when you ask.</strong>{" "}
              The extension has no standing access to any portal. When you click its toolbar
              button, the browser grants it access to that one tab, for that one action. It does
              not run in the background, it does not watch other tabs, and it holds no list of
              sites it is allowed on.
            </p>
            <p>
              <strong className="text-foreground">Your SimbaFlow account.</strong> It reads from
              this site using the session you are already signed in with, so it cannot see
              anything you could not see yourself.
            </p>
            <p>
              It also runs one small script on SimbaFlow&rsquo;s own pages, which does a single
              thing: it marks the page so the settings screen knows the extension is installed and
              stops offering you the download. It reads nothing.
            </p>
          </Section>

          <Section title="What it sends, and to whom">
            <p>
              <strong className="text-foreground">Nothing goes anywhere except SimbaFlow.</strong>{" "}
              The extension talks to your own agency&rsquo;s SimbaFlow server and to no other
              service. There is no analytics, no tracking, no error reporting to a third party,
              and nothing is sold or shared.
            </p>
            <p>
              Candidate details travel from SimbaFlow to the form in front of you and stop there.
              They are not sent to us, stored by the extension, or kept once the tab is closed.
            </p>
          </Section>

          <Section title="What it stores">
            <p>
              One thing, and only if you change it: the address of the SimbaFlow server to use,
              which exists so the extension can be pointed at a test installation. No candidate
              data, no credentials, no record of what was filled. It holds no password or key of
              its own — it uses the session your browser already has.
            </p>
          </Section>

          <Section title="What it will not do">
            <ul className="space-y-2.5">
              {[
                [Ban, "It never submits a form. It fills boxes; you read them and press the button yourself."],
                [ShieldCheck, "It never ticks a checkbox or chooses a radio button. Declarations on a government application are yours to make."],
                [KeyRound, "It will not type into a dropdown that is not a standard one, because that leaves a box which reads correctly and has selected nothing. It names those for you instead."],
              ].map(([Icon, text]) => {
                const I = Icon as typeof Eye;
                return (
                  <li key={text as string} className="flex gap-2.5">
                    <I className="mt-0.5 size-4 shrink-0" />
                    <span>{text as string}</span>
                  </li>
                );
              })}
            </ul>
          </Section>

          <Section title="The permissions it asks for">
            <dl className="divide-y rounded-lg border">
              {[
                ["activeTab", "To fill the form in the tab you are looking at, at the moment you click the button."],
                ["scripting", "To put the values into that page's boxes."],
                ["storage", "To remember the server address described above."],
                ["Access to this site", "To ask SimbaFlow what a form wants and which candidate you chose."],
              ].map(([name, why]) => (
                <div key={name} className="grid gap-1 p-3.5 sm:grid-cols-[150px_1fr] sm:gap-4">
                  <dt className="text-foreground font-mono text-xs">{name}</dt>
                  <dd className="text-sm">{why}</dd>
                </div>
              ))}
            </dl>
          </Section>

          <Section title="Children">
            <p>
              This is a tool for agency staff at work. It is not directed at children and collects
              nothing from them.
            </p>
          </Section>

          <Section title="Changes, and getting in touch">
            <p>
              If what the extension does changes, this page changes with it and the date at the
              top moves. Questions, or anything that looks wrong here:{" "}
              <a
                className="text-foreground font-medium underline underline-offset-4"
                href={`mailto:${CONTACT}`}
              >
                {CONTACT}
              </a>
              .
            </p>
          </Section>
        </div>
      </main>
    </div>
  );
}
