import {
  type FieldKey,
  type SiteManifest,
  type ValueKind,
  type WriteField,
} from "./manifest";

/**
 * Turning a candidate into instructions for somebody else's form.
 *
 * Deliberately produces a plan rather than filling anything. Nothing here knows what an input
 * element is, which is what lets the same function serve the extension that fills the insurance
 * form and the panel that shows a person the values to type. It also means the interesting part
 * — which value goes in which box, and in what shape — is testable without a browser.
 *
 * The plan never includes submitting. The extension fills the boxes and a person reads them and
 * presses Add Member. Twenty clicks instead of three hundred and twenty keystrokes is the whole
 * win; taking the last click as well would buy nothing and would mean a wrong row filed under a
 * real person's name with nobody having looked at it.
 */

export type FillStep = {
  /** Absent for a box the agency answers the same way every time. */
  key?: FieldKey;
  label: string;
  kind: ValueKind;
  /**
   * The canonical value: ISO for a date, international for a phone. A native date input only
   * accepts ISO however it chooses to display itself, so this is what goes into one.
   */
  value: string;
  /** The same value written the way the form shows it, for a text box or for a person reading. */
  display: string;
  /**
   * Other spellings worth trying, for a dropdown. Matching an option is guesswork — "Saudi
   * Arabia", "KSA" and "Saudi" are the same destination — so the filler is given every spelling
   * rather than one and a hope.
   */
  alternatives: string[];
  /** Do not overwrite a box that already has something in it. */
  keepTheirs: boolean;
};

export type FillPlan = {
  steps: FillStep[];
  /**
   * Boxes left for a person: ones this candidate has nothing for, and ones the form asks for
   * that SimbaFlow does not hold at all. Named as the form names them, so the gap can be read
   * off the screen the person is looking at.
   */
  missing: { key?: FieldKey; label: string }[];
  repeatButton?: string;
};

export type CandidateValues = Partial<Record<FieldKey, string | number | null>>;

/** "Mrs." once married, "Ms." before — a form that asks for a title and offers no blank. */
export function titleFor(values: CandidateValues): string {
  const gender = String(values.gender ?? "").toLowerCase();
  if (gender.startsWith("m") && !gender.startsWith("mar")) return "Mr.";
  const married = String(values.maritalStatus ?? "").toLowerCase().startsWith("married");
  return married ? "Mrs." : "Ms.";
}

/**
 * The same number, spelt the ways a form might want it.
 *
 * Not every number here is Ethiopian, which is the trap: the candidate's is, and the sponsor's
 * is Saudi. Assuming the local convention turned the employer's `966…` into a number with no
 * country code on it at all — a phone number on a visa application that nobody can ring.
 */
export function phoneSpellings(raw: string): { value: string; display: string; others: string[] } {
  // One leading plus at most; a stray one in the middle is punctuation.
  const digits = raw.replace(/[^\d+]/g, "").replace(/(?!^)\+/g, "");
  if (!digits) return { value: "", display: "", others: [] };

  const both = (value: string, display: string) => ({
    value,
    display,
    others: Array.from(new Set([display, value, digits].filter(Boolean))),
  });

  if (digits.startsWith("+")) {
    return both(digits, digits.startsWith("+251") ? `0${digits.slice(4)}` : digits);
  }
  if (digits.startsWith("00")) {
    const international = `+${digits.slice(2)}`;
    return both(international, international.startsWith("+251") ? `0${international.slice(4)}` : international);
  }
  // A leading zero is the local convention, and locally means Ethiopia.
  if (digits.startsWith("0")) {
    return both(`+251${digits.slice(1)}`, digits);
  }
  // Long enough to be carrying its own country code already, as the Saudi numbers on these
  // pages do — they just arrive without the plus.
  if (digits.length >= 11) {
    return both(`+${digits}`, digits);
  }
  return both(digits, digits);
}

/** `1996-01-01` as a form that is not a date picker would print it. */
export function displayDate(iso: string, order: "mdy" | "dmy" = "mdy"): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return iso;
  const [, y, mm, dd] = m;
  return order === "mdy" ? `${mm}/${dd}/${y}` : `${dd}/${mm}/${y}`;
}

/**
 * Spellings of the same answer, for dropdowns that were not written with us in mind.
 *
 * Only the ones actually seen on these forms. A longer list is not better: every extra spelling
 * is another chance to match the wrong option confidently.
 */
const SYNONYMS: Record<string, string[]> = {
  female: ["Female", "F", "FEMALE", "Ms."],
  male: ["Male", "M", "MALE", "Mr."],
  ethiopia: ["Ethiopia", "ETHIOPIA", "ET", "ETH"],
  // The occupation an Ethiopian agency registers and the one a Saudi visa form offers are the
  // same job under different names, and the form will not take ours.
  "house maid": ["House Maid", "House Worker", "Housemaid", "Domestic Worker", "Housemaid/Cleaner"],
  housemaid: ["House Maid", "House Worker", "Housemaid", "Domestic Worker"],
  air: ["Air", "By Air", "Airplane", "Flight"],
  normal: ["Normal", "Ordinary", "Regular"],
  "saudi arabia": ["Saudi Arabia", "SAUDI ARABIA", "KSA", "Saudi", "SA"],
  "united arab emirates": ["United Arab Emirates", "UAE", "U.A.E", "Dubai"],
  kuwait: ["Kuwait", "KUWAIT", "KW"],
  qatar: ["Qatar", "QATAR", "QA"],
  bahrain: ["Bahrain", "BAHRAIN", "BH"],
  jordan: ["Jordan", "JORDAN", "JO"],
  lebanon: ["Lebanon", "LEBANON", "LB"],
};

function spellings(value: string): string[] {
  const key = value.trim().toLowerCase();
  const known = SYNONYMS[key] ?? [];
  return Array.from(new Set([value, ...known]));
}

/**
 * @param answers What this agency has chosen for the boxes it always answers the same way,
 *   keyed by the label on the form. Overrides the default the map ships; an absent or empty
 *   entry leaves that default alone, so an agency that has never opened the settings screen
 *   still files a correct application.
 */
export function fillPlan(
  values: CandidateValues,
  manifest: SiteManifest,
  answers: Record<string, string> = {},
): FillPlan {
  const fields = manifest.fields as WriteField[];
  const steps: FillStep[] = [];
  const missing: FillPlan["missing"] = [];

  for (const field of fields) {
    const fromRecord =
      field.key === "title"
        ? titleFor(values)
        : field.key
          ? String(values[field.key] ?? "").trim()
          : "";

    // The candidate first, then what this agency has chosen, then what the map ships. That
    // order matters on the fields where more than one exists: a candidate whose passport is not
    // a Normal one must not have "Normal" put back over it because that is what the agency
    // files most of the time.
    const raw = fromRecord || (answers[field.label] ?? "").trim() || (field.fixed ?? "");

    if (!raw) {
      missing.push({ key: field.key, label: field.label });
      continue;
    }

    const kind = field.kind ?? "text";
    let value = raw;
    let display = raw;
    let alternatives: string[] = [];

    if (kind === "date") {
      display = displayDate(raw);
    } else if (kind === "phone") {
      const spelt = phoneSpellings(raw);
      value = spelt.value;
      display = spelt.display;
      alternatives = spelt.others;
    } else if (kind === "select") {
      alternatives = spellings(raw);
    }

    steps.push({
      key: field.key,
      label: field.label,
      kind,
      value,
      display,
      alternatives,
      keepTheirs: field.keepTheirs ?? false,
    });
  }

  return { steps, missing, repeatButton: manifest.repeatButton };
}

/**
 * The plan as a person would read it, for the panel beside the form and for anyone without the
 * extension. Tab order, because that is the order their fingers will go in.
 */
export function asLines(plan: FillPlan): string[] {
  return plan.steps.map((s) => `${s.label}: ${s.display}`);
}
