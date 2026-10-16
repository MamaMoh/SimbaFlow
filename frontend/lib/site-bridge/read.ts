import { splitFullName, splitGivenNames } from "./names";
import {
  type FieldKey,
  type LeadField,
  type ReadField,
  type SiteManifest,
  type ValueKind,
} from "./manifest";

/**
 * Pulling a candidate out of a page that has been copied to the clipboard.
 *
 * Driven by the portal's own labels rather than by the shape of the values. A regex for "ten
 * digits beginning 19" finds the visa number right up until the day a sponsor ID happens to look
 * the same, and then it fills the wrong box silently. "Visa Number:" means the visa number on
 * every page that prints it, and when the portal stops printing it the field simply comes back
 * empty — which is a thing the person can see.
 *
 * The parse never fills anything on its own. It returns what it found, what it did not, and what
 * it saw but does not understand, and a person confirms. That last list is the useful one: it is
 * how a label we have not mapped yet makes itself known instead of being silently dropped.
 */

export type ReadResult = {
  values: Partial<Record<FieldKey, string>>;
  found: { key: FieldKey; label: string; value: string }[];
  missing: { key: FieldKey; label: string }[];
  /** Labels on the page with no field of ours behind them. */
  unrecognised: { label: string; value: string }[];
};

type Hit = {
  start: number;
  end: number;
  label: string;
  field?: ReadField;
};

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/**
 * Where every label sits in the text, known ones and strangers alike.
 *
 * Strangers are collected for a reason beyond reporting them: they are boundaries. A value runs
 * until the next label starts, so an unmapped "Skills:" sitting between two mapped labels has to
 * be found, or the field before it swallows both the label and its value.
 *
 * Labels also sit inside one another — "Address" is a label and so is "Relative address", and
 * the short one matches in the middle of the long one. Left alone that puts the relative's
 * address into the candidate's own address field, which is wrong in a way nobody would catch by
 * reading the result. So overlapping hits are resolved in favour of the longest label that
 * starts earliest, and a known label beats a stranger of the same length.
 *
 * Not every portal prints the colon. The candidate page puts the label on one line and the value
 * on the next and nothing between them, so a label standing alone on its own line counts too.
 * Only known labels are read that way: a page of bare lines gives nothing to tell a label from a
 * value, and guessing there would invent fields out of people's names.
 */
function labelHits(text: string, fields: ReadField[]): Hit[] {
  const all: Hit[] = [];

  for (const field of fields) {
    // Whitespace in a label is flexible because a copied page may wrap or tab where it printed
    // a single space.
    const pattern = new RegExp(`${escape(field.label).replace(/\s+/g, "\\s+")}\\s*:`, "gi");
    for (let m = pattern.exec(text); m; m = pattern.exec(text)) {
      all.push({ start: m.index, end: m.index + m[0].length, label: field.label, field });
    }

    // The same label with no colon, holding a line on its own. Its own line and no more: a label
    // sharing a line with its value is the colon case above, and a label found mid-sentence is
    // prose. Whitespace inside the label stays on one line for the same reason.
    const bare = new RegExp(
      `(?:^|\\n)[ \\t]*(${escape(field.label).replace(/\s+/g, "[ \\t]+")})[ \\t]*(?=\\n|$)`,
      "gi",
    );
    for (let m = bare.exec(text); m; m = bare.exec(text)) {
      const start = m.index + m[0].indexOf(m[1]);
      all.push({ start, end: start + m[1].length, label: field.label, field });
    }
  }

  // A stranger has to look like a label and not merely contain a colon: at the start of a line,
  // or after the run of space a column layout leaves behind. Without that, `10:30` reads as a
  // label. A `//` after the colon is a URL rather than an empty field.
  const generic = /(?:^|\n|\t|\u00a0| {2,})\s*([A-Za-z][A-Za-z0-9 .'/-]{0,40}?)\s*:/g;
  for (let m = generic.exec(text); m; m = generic.exec(text)) {
    const start = m.index + m[0].indexOf(m[1]);
    const end = m.index + m[0].length;
    if (text.slice(end, end + 2) === "//") continue;
    all.push({ start, end, label: m[1].trim() });
  }

  all.sort(
    (a, b) =>
      a.start - b.start ||
      b.end - b.start - (a.end - a.start) ||
      (a.field ? 0 : 1) - (b.field ? 0 : 1),
  );

  const kept: Hit[] = [];
  for (const hit of all) {
    if (kept.some((k) => hit.start < k.end && hit.end > k.start)) continue;
    kept.push(hit);
  }
  return kept;
}

/** Portals print one value per label; a copied page turns the layout into newlines and tabs. */
const tidy = (value: string) => value.replace(/\s+/g, " ").trim();

/**
 * The value, and nothing that merely follows it.
 *
 * A value runs until the next *label*, but a page is not only labels: it has headings. "Passport
 * Expiry Date:" is followed by the date, a blank line and the word "Address", which heads the
 * next section and carries no colon to be recognised by. Taken whole, the expiry date reads
 * "2034-01-31 Address".
 *
 * Every portal here prints one value per label, on the label's own line or the one under it, so
 * the first line with anything on it is the value and the rest is somebody else's.
 */
const firstLine = (slice: string) =>
  slice.split("\n").map((l) => l.trim()).find((l) => l.length > 0) ?? "";

/**
 * A date as the record holds it, which is ISO.
 *
 * Slash dates with no part above twelve are genuinely ambiguous, and this reads them day-first —
 * the convention on the portals these pages come from. A date that cannot be understood is
 * handed back untouched rather than guessed at, so it reaches the confirm step looking wrong
 * instead of arriving in the record looking plausible.
 */
export function toIsoDate(raw: string): string {
  const value = raw.trim();

  const iso = value.match(/^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$/);
  if (iso) {
    const [, y, m, d] = iso;
    return `${y}-${m.padStart(2, "0")}-${d.padStart(2, "0")}`;
  }

  const slash = value.match(/^(\d{1,2})[-/](\d{1,2})[-/](\d{4})$/);
  if (slash) {
    const [, a, b, y] = slash;
    const dayFirst = Number(a) > 12 ? true : Number(b) > 12 ? false : true;
    const day = dayFirst ? a : b;
    const month = dayFirst ? b : a;
    if (Number(day) > 31 || Number(month) > 12) return value;
    return `${y}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`;
  }

  return value;
}

/** Digits and a leading +, because a portal prints spaces and brackets that nothing wants. */
export const tidyPhone = (raw: string) => {
  const kept = raw.replace(/[^\d+]/g, "");
  return kept.startsWith("+") ? `+${kept.slice(1).replace(/\+/g, "")}` : kept.replace(/\+/g, "");
};

/**
 * A number the portal printed the way the country writes it, in the form a phone box reads.
 *
 * Musaned prints a Saudi mobile as 0500000000 — the national form, with the trunk zero and no
 * country code. A phone box already set to Saudi Arabia is handed those digits and takes them
 * for the whole number, so that one arrived in the form as +050 000 000 0, which is not a
 * number anybody can ring. The leading zero is the trunk prefix and the rest is the number:
 * dropping it and putting the dialling code in front is the whole of the rule.
 *
 * Anything that already carries a country code is left exactly as it is.
 */
export function toDestinationPhone(raw: string, dialCode = "966"): string {
  const digits = tidyPhone(raw);
  if (!digits) return digits;
  if (digits.startsWith("+")) return digits;
  if (digits.startsWith(dialCode)) return `+${digits}`;
  if (digits.startsWith("0")) return `+${dialCode}${digits.slice(1)}`;
  return `+${dialCode}${digits}`;
}

function normalise(value: string, kind?: ValueKind): string {
  const clean = tidy(value);
  if (kind === "date") return toIsoDate(clean);
  if (kind === "phone") return tidyPhone(clean);
  if (kind === "number") return clean.replace(/[^\d]/g, "");
  return clean;
}

/**
 * The shapes a portal presents that are not fields of ours.
 *
 * Ethiopian names are given, father, grandfather, and a passport splits them as given names and
 * a surname — so "HIWOT TADESSE" / "GEBRU" is first, middle, last in that order. The insurance
 * form asks for precisely those three under precisely those names, which is the whole of the
 * transform between the two sites.
 *
 * The dividing itself lives in names.ts, shared with the registration form, so that a name typed
 * into SimbaFlow's one box and a name pasted from a portal divide at the same place.
 */
function expand(key: FieldKey, value: string, into: Partial<Record<FieldKey, string>>): void {
  if (key === "givenNames") {
    const { firstName, middleName } = splitGivenNames(value);
    if (firstName) into.firstName = firstName;
    if (middleName) into.middleName = middleName;
    return;
  }

  // A portal that prints the whole name in one box. Divided by the same rule the registration
  // form uses on its own name box, so a candidate typed in by hand and one pasted from a portal
  // end up filed identically.
  if (key === "fullName") {
    const { firstName, middleName, lastName } = splitFullName(value);
    if (firstName) into.firstName = firstName;
    if (middleName) into.middleName = middleName;
    if (lastName) into.lastName = lastName;
    return;
  }

  if (key === "workExperienceSummary") {
    // "Saudi Arabia - 6 years"
    const m = value.match(/^(.*?)\s*[-–]\s*(\d+)\s*year/i);
    if (m) {
      into.worksIn = m[1].trim();
      into.experienceAbroadYears = m[2];
    } else {
      into.worksIn = value;
    }
    return;
  }

  into[key] = value;
}

/**
 * The line under a heading, when that is where the portal prints a value.
 *
 * Bounded by the first label that follows the heading: on the contract block the employer's
 * name sits between "EMPLOYER INFO" and "LOCATION:", and a heading with nothing but labels
 * under it has no value of its own. A line that is itself a label is not a value either, which
 * is what stops a heading printed directly above "Location:" from reading the word "Location"
 * as somebody's name.
 */
function readLead(source: string, lead: LeadField, firstLabelAt: (from: number) => number):
  string | null {
  const heading = new RegExp(
    `(?:^|\n)[ \t]*${escape(lead.after).replace(/\s+/g, "[ \t]+")}[ \t]*(?=\n|$)`,
    "i",
  );
  const found = heading.exec(source);
  if (!found) return null;

  const start = found.index + found[0].length;
  const until = firstLabelAt(start);
  const line = firstLine(source.slice(start, until));
  if (!line || /:\s*$/.test(line)) return null;
  return line;
}

/** Every line with something on it, in order. */
const lines = (slice: string) =>
  slice.split("\n").map((l) => l.trim()).filter((l) => l.length > 0);

/**
 * The values for a row of labels printed across the page rather than down it.
 *
 * The contract block prints Location, Visa Number and Mobile Number side by side: three labels
 * on one line and their three values on the next. Copied out of a table that comes back as all
 * three labels and then all three values, so every label but the last has nothing after it and
 * the last one has everything — which, read a line at a time, puts the address into the mobile
 * number.
 *
 * A run of labels with nothing between them, ended by one carrying at least as many lines as
 * the run is long, is that shape and nothing else is. Those lines are dealt out to those
 * labels in order. Anything short of that is left alone: a label with a genuinely empty value
 * is common, and guessing there would invent a value rather than report a gap.
 */
function columnar(source: string, hits: Hit[]): Map<number, string> {
  const paired = new Map<number, string>();
  const after = (i: number) => source.slice(hits[i].end, hits[i + 1]?.start ?? source.length);

  for (let i = 0; i < hits.length; i++) {
    if (firstLine(after(i)) !== "") continue;

    let end = i;
    while (end + 1 < hits.length && firstLine(after(end + 1)) === "") end++;

    const last = end + 1;
    if (last >= hits.length) break;

    const values = lines(after(last));
    const run = last - i + 1;
    if (values.length < run) {
      i = end;
      continue;
    }

    for (let k = 0; k < run; k++) paired.set(i + k, values[k]);
    i = last;
  }

  return paired;
}

/** Digits enough to ring. An address that landed in a phone box is not a phone number. */
const looksLikeAPhone = (value: string) => value.replace(/\D/g, "").length >= 7;

export function readFromText(text: string, manifest: SiteManifest): ReadResult {
  const fields = manifest.fields as ReadField[];
  const source = text.replace(/\r\n?/g, "\n").replace(/ /g, " ");
  const hits = labelHits(source, fields);

  const values: Partial<Record<FieldKey, string>> = {};
  const found: ReadResult["found"] = [];
  const unrecognised: ReadResult["unrecognised"] = [];
  const seen = new Set<FieldKey>();

  const paired = columnar(source, hits);

  hits.forEach((hit, i) => {
    const until = hits[i + 1]?.start ?? source.length;
    const raw = paired.get(i) ?? firstLine(source.slice(hit.end, until));
    if (!hit.field) {
      const value = tidy(raw);
      if (value) unrecognised.push({ label: hit.label, value });
      return;
    }

    const value = normalise(raw, hit.field.kind);
    // A label printed twice — once in a summary and once in detail — keeps its first value.
    // Later ones are usually the emptier of the two.
    if (!value || seen.has(hit.field.key)) return;
    // A phone box is the one that quietly accepts something that is not its value: every
    // other field shows what it got, while an address stripped to its digits reads as a
    // number somebody could believe.
    if (hit.field.kind === "phone" && !looksLikeAPhone(value)) return;

    seen.add(hit.field.key);
    expand(hit.field.key, value, values);
    found.push({ key: hit.field.key, label: hit.field.label, value });
  });

  for (const lead of manifest.leads ?? []) {
    if (seen.has(lead.key)) continue;
    const raw = readLead(source, lead, (from) =>
      hits.find((h) => h.start >= from)?.start ?? source.length,
    );
    if (!raw) continue;
    const value = normalise(raw, lead.kind);
    if (!value) continue;
    seen.add(lead.key);
    expand(lead.key, value, values);
    found.push({ key: lead.key, label: lead.after, value });
  }

  const missing = [
    ...fields.map((f) => ({ key: f.key, label: f.label })),
    ...(manifest.leads ?? []).map((l) => ({ key: l.key, label: l.after })),
  ].filter((f) => !seen.has(f.key));

  return { values, found, missing, unrecognised };
}

/**
 * Which page this is, worked out from the page itself.
 *
 * Asking the person to say whether they copied the candidate page or the contract page is asking
 * them to think about our filing rather than their work, and to be wrong occasionally. The
 * labels on the page already answer it: whichever map recognises more of them is the one.
 *
 * Ties and near-ties go to neither — a paste that matches two maps equally is usually a paste of
 * something else entirely, and a confident wrong answer is worse here than no answer.
 */
export function bestRead(
  text: string,
  manifests: SiteManifest[],
): { manifest: SiteManifest; result: ReadResult } | null {
  const scored = manifests
    .filter((m) => m.direction === "read")
    .map((manifest) => ({ manifest, result: readFromText(text, manifest) }))
    .sort((a, b) => b.result.found.length - a.result.found.length);

  const best = scored[0];
  if (!best || best.result.found.length === 0) return null;
  return best;
}
