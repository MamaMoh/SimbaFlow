import type { Metadata } from "next";

/**
 * The privacy policy for the browser helper.
 *
 * Public, and it has to be: the Chrome Web Store will not list an extension whose policy sits
 * behind a sign-in, and a policy only the people who already have the thing can read is not a
 * policy. /privacy is in the middleware's public routes for that reason.
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

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      <div className="space-y-3 text-sm leading-relaxed text-slate-700">{children}</div>
    </section>
  );
}

export default function PrivacyPage() {
  return (
    <main className="mx-auto max-w-2xl px-6 py-14">
      <header className="mb-10 space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900">
          SimbaFlow Form Filler — privacy
        </h1>
        <p className="text-sm text-slate-500">Last updated {UPDATED}</p>
      </header>

      <div className="space-y-9">
        <Section title="What it is for">
          <p>
            The SimbaFlow Form Filler is a browser extension for staff at labour-export agencies
            who use SimbaFlow. It types a candidate&rsquo;s details — the ones the agency already
            holds — into the insurance and visa portals, so that nobody has to copy sixteen boxes
            by hand for each person.
          </p>
        </Section>

        <Section title="What it can see">
          <p>
            <strong>The page you are on, only when you ask.</strong> The extension has no standing
            access to any portal. When you click its toolbar button, the browser grants it access
            to that one tab, for that one action. It does not run in the background, it does not
            watch other tabs, and it has no list of sites it is allowed on.
          </p>
          <p>
            <strong>Your SimbaFlow account.</strong> It reads from{" "}
            <code className="rounded bg-slate-100 px-1 py-0.5 text-xs">app.laba.et</code> using the
            session you are already signed in with. It cannot see anything there that you cannot
            see yourself.
          </p>
          <p>
            It also runs one small script on SimbaFlow&rsquo;s own pages, which does a single
            thing: it marks the page so that the settings screen knows the extension is installed
            and stops offering you the download. It reads nothing.
          </p>
        </Section>

        <Section title="What it sends, and to whom">
          <p>
            <strong>Nothing goes anywhere except SimbaFlow.</strong> The extension talks to your
            own agency&rsquo;s SimbaFlow server and to no other service. There is no analytics, no
            tracking, no error reporting to a third party, and nothing is sold or shared with
            anyone.
          </p>
          <p>
            Candidate details travel from SimbaFlow to the form in front of you and stop there.
            They are not sent to us, stored by the extension, or kept after the tab is closed.
          </p>
        </Section>

        <Section title="What it stores">
          <p>
            One thing, and only if you change it: the address of the SimbaFlow server to use,
            which exists so the extension can be pointed at a test installation. No candidate
            data, no credentials, no history of what was filled. It holds no password or key of
            its own — it uses the session cookie your browser already has.
          </p>
        </Section>

        <Section title="What it will not do">
          <ul className="list-disc space-y-1.5 pl-5">
            <li>
              It never submits a form. It fills boxes; you read them and press the button
              yourself.
            </li>
            <li>
              It never ticks a checkbox or chooses a radio button. Declarations on a government
              application are yours to make.
            </li>
            <li>
              It will not type into a dropdown that is not a standard one, because doing so leaves
              a box that reads correctly and has selected nothing. It tells you about those
              instead.
            </li>
          </ul>
        </Section>

        <Section title="The permissions it asks for">
          <ul className="list-disc space-y-1.5 pl-5">
            <li>
              <strong>activeTab</strong> — to fill the form in the tab you are looking at, at the
              moment you click the button.
            </li>
            <li>
              <strong>scripting</strong> — to put the values into that page&rsquo;s boxes.
            </li>
            <li>
              <strong>storage</strong> — to remember the server address described above.
            </li>
            <li>
              <strong>Access to app.laba.et</strong> — to ask SimbaFlow what a form wants and which
              candidate you chose.
            </li>
          </ul>
        </Section>

        <Section title="Children">
          <p>
            This is a tool for agency staff at work. It is not directed at children and collects
            nothing from them.
          </p>
        </Section>

        <Section title="Changes, and getting in touch">
          <p>
            If what the extension does changes, this page changes with it, and the date at the top
            moves. Questions, or anything that looks wrong here:{" "}
            <a className="font-medium text-emerald-800 underline" href={`mailto:${CONTACT}`}>
              {CONTACT}
            </a>
            .
          </p>
        </Section>
      </div>
    </main>
  );
}
