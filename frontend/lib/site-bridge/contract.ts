/**
 * The two things the LMIS Create Contract form asks for that SimbaFlow holds only the parts of:
 * when the contract ends, and which city the employer is in.
 *
 * Neither is stored, and neither should be. The end date is the start date plus the term, and a
 * stored copy is a second date to disagree with the first the day somebody corrects either. The
 * city is already in the sponsor's address — it arrives off the Musaned contract page in Arabic,
 * kept as it came — and the LMIS dropdown wants it on its own, in English.
 */

/** Years in a contract when the record says nothing, as the intake form fills it in. */
const DEFAULT_TERM_YEARS = 2;

const WORD_NUMBERS: Record<string, number> = { one: 1, two: 2, three: 3, four: 4, five: 5 };

/**
 * The term in months, read the way ContractTerm on the backend reads it: a bare number is years,
 * and anything already in words is taken at its word — "24 months", "Two years".
 *
 * Null for an answer that cannot be read, which leaves the end date to a person rather than
 * filing a government contract with a two-year guess over a three-year term.
 */
export function termMonths(period: string | null | undefined): number | null {
  const text = String(period ?? "").trim().toLowerCase();
  if (!text) return DEFAULT_TERM_YEARS * 12;
  const digits = text.match(/^\d+/)?.[0];
  const word = text.match(/^[a-z]+/)?.[0];
  const n = digits ? parseInt(digits, 10) : word ? WORD_NUMBERS[word] : undefined;
  if (!n || n <= 0) return null;
  return /month/.test(text) ? n : n * 12;
}

/**
 * `2026-03-01` and a two-year term → `2028-03-01`.
 *
 * The same day of the month, the term later, which is how the contract itself counts. A start on
 * the 29th of February ends on the 28th in a year that has no 29th, rather than rolling into March.
 */
export function contractEnd(startIso: string, period: string | null | undefined): string {
  const m = String(startIso ?? "").match(/^(\d{4})-(\d{2})-(\d{2})/);
  const months = termMonths(period);
  if (!m || months === null) return "";
  const total = parseInt(m[1], 10) * 12 + (parseInt(m[2], 10) - 1) + months;
  const year = Math.floor(total / 12);
  const month = total % 12;
  const lastDay = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const day = Math.min(parseInt(m[3], 10), lastDay);
  const pad = (v: number) => String(v).padStart(2, "0");
  return `${year}-${pad(month + 1)}-${pad(day)}`;
}

/**
 * Saudi cities as the LMIS dropdown is likely to print them, and the ways an address spells them.
 *
 * Only the places sponsors actually live. Bare "المدينة" is left out on purpose: on its own it is
 * "the city", and an address that says so would send the contract to Medina.
 */
export const SAUDI_CITIES: { name: string; spellings: string[] }[] = [
  { name: "Riyadh", spellings: ["الرياض", "Riyadh", "Riyad", "Ar Riyadh"] },
  { name: "Jeddah", spellings: ["جدة", "جده", "Jeddah", "Jiddah", "Jedda"] },
  { name: "Mecca", spellings: ["مكة", "مكه", "Mecca", "Makkah", "Makka"] },
  { name: "Medina", spellings: ["المدينة المنورة", "المدينه المنوره", "Medina", "Madinah", "Al Madinah"] },
  { name: "Dammam", spellings: ["الدمام", "Dammam", "Ad Dammam"] },
  { name: "Khobar", spellings: ["الخبر", "Khobar", "Al Khobar"] },
  { name: "Dhahran", spellings: ["الظهران", "Dhahran"] },
  { name: "Taif", spellings: ["الطائف", "Taif", "At Taif"] },
  { name: "Abha", spellings: ["أبها", "ابها", "Abha"] },
  { name: "Khamis Mushait", spellings: ["خميس مشيط", "Khamis Mushait", "Khamis Mushayt"] },
  { name: "Tabuk", spellings: ["تبوك", "Tabuk"] },
  { name: "Buraidah", spellings: ["بريدة", "بريده", "Buraidah", "Buraydah"] },
  { name: "Unaizah", spellings: ["عنيزة", "عنيزه", "Unaizah", "Unayzah"] },
  { name: "Hail", spellings: ["حائل", "Hail", "Ha'il"] },
  { name: "Jazan", spellings: ["جازان", "جيزان", "Jazan", "Jizan", "Gizan"] },
  { name: "Najran", spellings: ["نجران", "Najran"] },
  { name: "Al Ahsa", spellings: ["الأحساء", "الاحساء", "الهفوف", "Al Ahsa", "Al Hasa", "Hofuf", "Al Hofuf"] },
  { name: "Jubail", spellings: ["الجبيل", "Jubail", "Al Jubail"] },
  { name: "Yanbu", spellings: ["ينبع", "Yanbu"] },
  { name: "Al Kharj", spellings: ["الخرج", "Al Kharj", "Kharj"] },
  { name: "Qatif", spellings: ["القطيف", "Qatif", "Al Qatif"] },
  { name: "Hafar Al Batin", spellings: ["حفر الباطن", "Hafar Al Batin", "Hafr Al Batin"] },
  { name: "Arar", spellings: ["عرعر", "Arar"] },
  { name: "Sakaka", spellings: ["سكاكا", "Sakaka"] },
  { name: "Al Baha", spellings: ["الباحة", "الباحه", "Al Baha", "Baha"] },
];

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/**
 * The one city an address names, in English; empty when it names none, or more than one.
 *
 * Two cities in one address is an address that says "Riyadh, near the Jeddah road", and picking
 * either is a guess on a government contract. The person chooses it instead.
 */
export function saudiCityIn(address: string | null | undefined): string {
  const text = String(address ?? "");
  if (!text.trim()) return "";
  const found = SAUDI_CITIES.filter(({ spellings }) =>
    spellings.some((s) =>
      // Not inside a longer word, in either script. \b knows nothing about Arabic letters.
      new RegExp(`(?<!\\p{L})${escape(s)}(?!\\p{L})`, "iu").test(text),
    ),
  );
  return found.length === 1 ? found[0].name : "";
}
