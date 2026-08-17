import { createWorker, PSM } from "tesseract.js";
import { parse as parseMrz } from "mrz";

export type PassportOcrResult = {
  firstName: string;
  middleName: string;
  lastName: string;
  passportNumber: string;
  dateOfBirth: string;
  gender: string; // "0" male | "1" female
  nationality: string;
  passportExpiryDate: string;
  passportPlaceOfIssue: string;
  placeOfBirth?: string;
  passportIssueDate?: string;
  passportType: string;
  rawNationalityCode?: string;
  confidence: "high" | "medium" | "low";
};

const ALPHA3_TO_NAME: Record<string, string> = {
  ETH: "Ethiopia",
  ERI: "Eritrea",
  SOM: "Somalia",
  KEN: "Kenya",
  SDN: "Sudan",
  SSD: "South Sudan",
  DJI: "Djibouti",
  UGA: "Uganda",
  TZA: "Tanzania",
  SAU: "Saudi Arabia",
  ARE: "United Arab Emirates",
  QAT: "Qatar",
  KWT: "Kuwait",
  BHR: "Bahrain",
  OMN: "Oman",
  JOR: "Jordan",
  EGY: "Egypt",
  IND: "India",
  PHL: "Philippines",
  PAK: "Pakistan",
  BGD: "Bangladesh",
  USA: "United States",
  GBR: "United Kingdom",
  CAN: "Canada",
  DEU: "Germany",
  FRA: "France",
  ITA: "Italy",
  TUR: "Turkey",
  CHN: "China",
};

const MRZ_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<";

function alpha3ToCountryName(code: string | null | undefined): string {
  if (!code) return "";
  const clean = code.replace(/</g, "").toUpperCase();
  return ALPHA3_TO_NAME[clean] || clean;
}

function mrzDateToIso(yymmdd: string | null | undefined, preferFuture = false): string {
  if (!yymmdd || !/^\d{6}$/.test(yymmdd)) return "";
  const yy = Number(yymmdd.slice(0, 2));
  const mm = Number(yymmdd.slice(2, 4));
  const dd = Number(yymmdd.slice(4, 6));
  if (mm < 1 || mm > 12 || dd < 1 || dd > 31) return "";
  const century = preferFuture ? (yy <= 50 ? 2000 : 1900) : yy <= 30 ? 2000 : 1900;
  return `${century + yy}-${String(mm).padStart(2, "0")}-${String(dd).padStart(2, "0")}`;
}

function sexToGender(sex: string | null | undefined): string {
  const s = (sex || "").toLowerCase();
  if (s.startsWith("f")) return "1";
  if (s.startsWith("m")) return "0";
  return "";
}

function splitGivenNames(firstName: string): { firstName: string; middleName: string } {
  const parts = firstName.trim().split(/\s+/).filter(Boolean);
  if (parts.length <= 1) return { firstName: parts[0] || "", middleName: "" };
  return { firstName: parts[0], middleName: parts.slice(1).join(" ") };
}

function charValue(c: string): number {
  if (c === "<") return 0;
  if (c >= "0" && c <= "9") return c.charCodeAt(0) - 48;
  if (c >= "A" && c <= "Z") return c.charCodeAt(0) - 55;
  return -1;
}

function computeCheckDigit(data: string): string {
  const weights = [7, 3, 1];
  let sum = 0;
  for (let i = 0; i < data.length; i++) {
    const v = charValue(data[i]);
    if (v < 0) return "";
    sum += v * weights[i % 3];
  }
  return String(sum % 10);
}

function checkOk(data: string, digit: string): boolean {
  return computeCheckDigit(data) === digit;
}

/** Normalize OCR noise before structural parsing. */
function normalizeMrzNoise(raw: string): string {
  return raw
    .toUpperCase()
    .replace(/\s+/g, "")
    .replace(/[|!]/g, "I")
    .replace(/[«»„“”"'`´{\[(}\])]/g, "<")
    .replace(/CK/g, "<<")
    .replace(/KK/g, "<<")
    .replace(/[^A-Z0-9<]/g, "");
}

function padLine(line: string, length: number): string {
  let s = normalizeMrzNoise(line);
  if (s.length > length) s = s.slice(0, length);
  while (s.length < length) s += "<";
  return s;
}

function extractCandidateLines(ocrText: string): string[] {
  return ocrText
    .split(/\r?\n/)
    .map((l) => normalizeMrzNoise(l))
    .filter((l) => l.length >= 20);
}

/**
 * Find TD3 (44+44) or TD2 (36+36) MRZ lines from OCR text.
 * Also accepts a single concatenated 88-char string.
 */
function extractMrzLines(ocrText: string): string[] | null {
  const candidates = extractCandidateLines(ocrText);

  // Direct TD3 pair
  for (let i = 0; i < candidates.length - 1; i++) {
    for (let j = i + 1; j < Math.min(i + 4, candidates.length); j++) {
      const a = padLine(fixLine1(candidates[i]), 44);
      const b = padLine(fixLine2(candidates[j]), 44);
      if (looksLikeTd3(a, b)) return [a, b];
    }
  }

  // Concatenated 88 chars somewhere in text
  const flat = normalizeMrzNoise(ocrText);
  const pIdx = flat.search(/P[<A-Z0-9]{40,}/);
  if (pIdx >= 0) {
    const slice = flat.slice(pIdx, pIdx + 88);
    if (slice.length >= 88) {
      const a = padLine(fixLine1(slice.slice(0, 44)), 44);
      const b = padLine(fixLine2(slice.slice(44, 88)), 44);
      if (looksLikeTd3(a, b)) return [a, b];
    }
  }

  // Last two long lines as fallback
  const long = candidates.filter((l) => l.length >= 36).slice(-2);
  if (long.length === 2) {
    const a = padLine(fixLine1(long[0]), 44);
    const b = padLine(fixLine2(long[1]), 44);
    if (/^P/.test(a)) return [a, b];
  }

  return null;
}

function fixLine1(line: string): string {
  let s = normalizeMrzNoise(line);
  // Document type must be P<
  if (s.startsWith("R<") || s.startsWith("F<") || s.startsWith("B<")) s = "P<" + s.slice(2);
  if (s.startsWith("PC")) s = "P<" + s.slice(2);
  if (s.startsWith("P=") || s.startsWith("P-")) s = "P<" + s.slice(2);
  if (!s.startsWith("P")) {
    const idx = s.indexOf("P<");
    if (idx >= 0) s = s.slice(idx);
    else if (s.startsWith("ETH") || s.startsWith("UTO")) s = "P<" + s;
  }
  // Country: common ETH misreads
  if (s.length >= 5) {
    let country = s.slice(2, 5);
    country = country
      .replace(/0/g, "O")
      .replace(/1/g, "I")
      .replace(/8/g, "B")
      .replace(/5/g, "S");
    if (country === "EIH" || country === "ETN" || country === "E7H" || country === "EIH")
      country = "ETH";
    if (country === "THT" || country === "E TH".replace(" ", "")) country = "ETH";
    s = s.slice(0, 2) + country + s.slice(5);
  }
  // Restore << separators: CK / CC / KK often = <<
  s = s.replace(/<</g, "<<").replace(/CK/g, "<<").replace(/KK/g, "<<").replace(/CC(?=[A-Z])/g, "<<");
  return s;
}

function fixLine2(line: string): string {
  let s = normalizeMrzNoise(line);
  // Ethiopian passport numbers start with EP
  if (/^[EF8B][PR]/.test(s)) {
    s = "EP" + s.slice(2);
  }
  return s;
}

function looksLikeTd3(a: string, b: string): boolean {
  return a.length === 44 && b.length === 44 && /^P[A-Z<]/.test(a);
}

function digitFix(s: string): string {
  return s.replace(/O/g, "0").replace(/I/g, "1").replace(/L/g, "1").replace(/S/g, "5").replace(/B/g, "8").replace(/Z/g, "2");
}

function isPlausibleMrzDate(yymmdd: string): boolean {
  if (!/^\d{6}$/.test(yymmdd)) return false;
  const mm = Number(yymmdd.slice(2, 4));
  const dd = Number(yymmdd.slice(4, 6));
  return mm >= 1 && mm <= 12 && dd >= 1 && dd <= 31;
}

function repairField(data: string, expectedCheck: string, preferDigit: boolean): string {
  if (checkOk(data, expectedCheck)) return data;

  const pairs: [string, string][] = preferDigit
    ? [
        ["O", "0"],
        ["I", "1"],
        ["L", "1"],
        ["Z", "2"],
        ["S", "5"],
        ["B", "8"],
        ["G", "6"],
        ["Q", "0"],
        ["D", "0"],
        ["A", "4"],
        ["T", "7"],
        ["E", "3"],
        ["F", "3"],
      ]
    : [
        ["0", "O"],
        ["1", "I"],
        ["8", "B"],
        ["5", "S"],
        ["2", "Z"],
        ["6", "G"],
        ["3", "E"],
      ];

  const chars = data.split("");
  for (let i = 0; i < chars.length; i++) {
    for (const [from, to] of pairs) {
      if (chars[i] !== from) continue;
      const next = chars.slice();
      next[i] = to;
      const candidate = next.join("");
      if (checkOk(candidate, expectedCheck)) return candidate;
    }
  }

  for (let i = 0; i < chars.length; i++) {
    for (const [f1, t1] of pairs) {
      if (chars[i] !== f1) continue;
      for (let j = i + 1; j < chars.length; j++) {
        for (const [f2, t2] of pairs) {
          if (chars[j] !== f2) continue;
          const next = chars.slice();
          next[i] = t1;
          next[j] = t2;
          const candidate = next.join("");
          if (checkOk(candidate, expectedCheck)) return candidate;
        }
      }
    }
  }

  return data;
}

/**
 * Repair a 6-digit MRZ date + its check digit.
 * OCR often flips B↔8 and also corrupts the check digit — if the date looks
 * plausible after letter→digit fixes, recompute the check digit.
 */
function repairDateWithCheck(data: string, checkDigit: string): { data: string; check: string } {
  let d = digitFix(data);
  if (checkOk(d, checkDigit)) return { data: d, check: checkDigit };

  const repaired = repairField(d, checkDigit, true);
  if (checkOk(repaired, checkDigit)) return { data: repaired, check: checkDigit };

  if (isPlausibleMrzDate(d)) {
    return { data: d, check: computeCheckDigit(d) };
  }
  if (isPlausibleMrzDate(repaired)) {
    return { data: repaired, check: computeCheckDigit(repaired) };
  }
  return { data: d, check: checkDigit };
}

function repairTd3Lines(line1: string, line2: string): [string, string] {
  let a = padLine(fixLine1(line1), 44);
  let b = padLine(fixLine2(line2), 44);

  // Document number (0-8) + check (9)
  let doc = repairField(b.slice(0, 9), b[9], true);
  if (!checkOk(doc, b[9])) {
    doc = repairField(b.slice(0, 9), b[9], false);
  }
  // Ethiopian passports: EP + digits — fix leading EP and recompute check if needed
  if (/^[EF8B][PR]/.test(doc)) doc = "EP" + doc.slice(2);
  doc = doc.slice(0, 2) + digitFix(doc.slice(2));
  let docCheck = b[9];
  if (!checkOk(doc, docCheck) && /^[A-Z0-9]{9}$/.test(doc)) {
    docCheck = computeCheckDigit(doc);
  }
  b = doc + docCheck + b.slice(10);

  // Nationality (10-12): force letters
  let nat = b
    .slice(10, 13)
    .replace(/0/g, "O")
    .replace(/1/g, "I")
    .replace(/8/g, "B")
    .replace(/5/g, "S")
    .replace(/3/g, "E");
  if (nat === "EIH" || nat === "ETN" || nat === "THT" || nat === "E7H" || nat === "6TH" || nat === "THE")
    nat = "ETH";
  b = b.slice(0, 10) + nat + b.slice(13);

  // DOB (13-18) + check (19)
  const dob = repairDateWithCheck(b.slice(13, 19), b[19]);
  b = b.slice(0, 13) + dob.data + dob.check + b.slice(20);

  // Sex (20)
  let sex = b[20];
  if (sex === "P") sex = "F";
  if (sex === "N" || sex === "H") sex = "M";
  if (sex !== "M" && sex !== "F" && sex !== "<") {
    sex = b[20];
  }
  b = b.slice(0, 20) + sex + b.slice(21);

  // Expiry (21-26) + check (27)
  const exp = repairDateWithCheck(b.slice(21, 27), b[27]);
  b = b.slice(0, 21) + exp.data + exp.check + b.slice(28);

  // Name separators: OCR often reads < as C/K in the name field
  if (a.length > 5) {
    const head = a.slice(0, 5);
    let names = a.slice(5);
    names = names.replace(/CK/g, "<<").replace(/KK/g, "<<").replace(/CC(?=[A-Z])/g, "<<");
    const firstSep = names.indexOf("<<");
    if (firstSep >= 0) {
      const surname = names.slice(0, firstSep + 2);
      let given = names.slice(firstSep + 2).replace(/C/g, "<").replace(/K/g, "<");
      // Drop lone OCR junk letters stuck between fillers (…TENAN<R<<<<)
      given = given.replace(/<+[A-Z](?=<)/g, "<");
      given = given.replace(/<{2,}/g, "<");
      names = surname + given;
    }
    a = padLine(head + names, 44);
  }

  // Issuing state on line 1
  if (a.length >= 5) {
    let iss = a
      .slice(2, 5)
      .replace(/0/g, "O")
      .replace(/1/g, "I")
      .replace(/8/g, "B");
    if (iss === "EIH" || iss === "THT" || iss === "ETN") iss = "ETH";
    a = a.slice(0, 2) + iss + a.slice(5);
  }

  return [a, b];
}

/** Visual-zone helpers (not in MRZ): place of birth, issue date, name confirmation. */
const MON: Record<string, string> = {
  JAN: "01",
  FEB: "02",
  MAR: "03",
  APR: "04",
  MAY: "05",
  JUN: "06",
  JUL: "07",
  AUG: "08",
  SEP: "09",
  OCT: "10",
  NOV: "11",
  DEC: "12",
};

function visualDateToIso(dd: string, mon: string, yy: string): string {
  const mm = MON[mon.toUpperCase()];
  if (!mm) return "";
  const y = Number(yy);
  if (!Number.isFinite(y)) return "";
  const century = y <= 30 ? 2000 : 1900;
  return `${century + y}-${mm}-${dd.padStart(2, "0")}`;
}

function extractVisualExtras(ocrText: string): {
  placeOfBirth?: string;
  passportIssueDate?: string;
  nameTokens: string[];
} {
  const upper = ocrText.toUpperCase();
  const out: ReturnType<typeof extractVisualExtras> = { nameTokens: [] };

  const dates = [
    ...upper.matchAll(/\b(\d{1,2})\s*(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s*(\d{2})\b/g),
  ].map((m) => ({
    raw: m[0],
    iso: visualDateToIso(m[1], m[2], m[3]),
    index: m.index ?? 0,
  })).filter((d) => d.iso);

  // Prefer a date near an "ISSUE" label when present
  const issueLabel = upper.search(/\b(?:DATE\s+OF\s+)?ISSUE\b/);
  if (issueLabel >= 0) {
    const near = dates
      .filter((d) => d.index >= issueLabel && d.index < issueLabel + 80)
      .sort((a, b) => a.index - b.index)[0];
    if (near?.iso) out.passportIssueDate = near.iso;
  }

  // Typical biodata layout: DOB, Issue, Expiry
  if (!out.passportIssueDate && dates.length >= 3) {
    out.passportIssueDate = dates[1].iso;
  } else if (!out.passportIssueDate && dates.length === 2) {
    // Prefer the later calendar date as issue (DOB is usually earlier)
    const sorted = [...dates].sort((a, b) => a.iso.localeCompare(b.iso));
    out.passportIssueDate = sorted[1]?.iso;
  }

  const pob = upper.match(
    /\d{1,2}\s*(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s*\d{2}\s+([A-Z]{3,20})/
  );
  if (pob?.[1] && !["ETHIOPIAN", "FEMALE", "MALE", "PASSPORT"].includes(pob[1])) {
    let place = pob[1]
      .replace(/ASQBOT/g, "ASOBOT")
      .replace(/[^A-Z]/g, "")
      .replace(/(CORC|CRC|CORD|ORG)$/g, "");
    if (place.length >= 3 && place.length <= 20) out.placeOfBirth = place;
  }

  // Tokens from visual zone only (exclude MRZ lines — they may contain OCR errors)
  const visualOnly = upper
    .split(/\r?\n/)
    .filter((line) => {
      const compact = line.replace(/[^A-Z0-9]/g, "");
      // Drop MRZ lines (including OCR variants like PCETH…)
      if (line.includes("<<")) return false;
      if (/^P[C<A-Z]/.test(compact) && compact.length >= 20) return false;
      if (compact.length >= 28 && /^[A-Z0-9]{8,9}\d?[A-Z]{3}/.test(compact)) return false;
      return true;
    })
    .join("\n");

  const skip = new Set([
    "PASSPORT",
    "ETHIOPIAN",
    "FEDERAL",
    "DEMOCRATIC",
    "REPUBLIC",
    "ETHIOPIA",
    "TYPE",
    "CODE",
    "SURNAME",
    "GIVEN",
    "NAME",
    "NATIONALITY",
    "SEX",
    "DATE",
    "BIRTH",
    "PLACE",
    "ISSUE",
    "EXPIRY",
    "AUTHORITY",
    "MAIN",
    "DEPARTMENT",
    "IMMIGRATION",
    "AND",
    "AFFAIRS",
    "FEMALE",
    "MALE",
  ]);
  out.nameTokens = (visualOnly.match(/[A-Z]{3,15}/g) || []).filter((t) => !skip.has(t));

  return out;
}

function confusableEqual(a: string, b: string): boolean {
  if (a === b) return true;
  const pairs = "NM|MN|O0|0O|I1|1I|L1|1L|B8|8B|S5|5S|Z2|2Z|G6|6G";
  return pairs.includes(a + b);
}

/** Edit distance that treats common OCR confusions as cheaper. */
function ocrNameDistance(a: string, b: string): number {
  if (a.length !== b.length) return levenshtein(a, b);
  let d = 0;
  for (let i = 0; i < a.length; i++) {
    if (a[i] === b[i]) continue;
    d += confusableEqual(a[i], b[i]) ? 0.5 : 1;
  }
  return d;
}

function closestToken(token: string, corpus: string[]): string {
  if (!token || token.length < 3) return token;
  let best = token;
  let bestD = 1.1; // allow up to two OCR confusions (e.g. N↔M twice: TENAN→TEMAM)
  const consider = (w: string) => {
    if (w.length !== token.length) return;
    const d = ocrNameDistance(token, w);
    if (d > 0 && d < bestD) {
      best = w;
      bestD = d;
    }
  };
  for (const w of corpus) {
    consider(w);
    if (w.length > token.length) {
      for (let i = 0; i <= w.length - token.length; i++) {
        consider(w.slice(i, i + token.length));
      }
    }
  }
  return best;
}

function refineNamesFromVisual(
  lastName: string,
  firstName: string,
  middleName: string,
  visual: ReturnType<typeof extractVisualExtras>
): { lastName: string; firstName: string; middleName: string } {
  const corpus = visual.nameTokens;
  const ln = closestToken(lastName, corpus);
  const fn = closestToken(firstName, corpus);
  const mn = middleName
    ? closestToken(middleName, corpus)
    : "";
  return { lastName: ln, firstName: fn, middleName: mn };
}

function levenshtein(a: string, b: string): number {
  const m = a.length;
  const n = b.length;
  const dp = Array.from({ length: m + 1 }, () => new Array<number>(n + 1).fill(0));
  for (let i = 0; i <= m; i++) dp[i][0] = i;
  for (let j = 0; j <= n; j++) dp[0][j] = j;
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      dp[i][j] =
        a[i - 1] === b[j - 1]
          ? dp[i - 1][j - 1]
          : 1 + Math.min(dp[i - 1][j], dp[i][j - 1], dp[i - 1][j - 1]);
    }
  }
  return dp[m][n];
}

function fieldMap(details: { field?: string | null; value?: string | null }[]): Record<string, string> {
  const out: Record<string, string> = {};
  for (const d of details) {
    if (d.field && d.value != null && d.value !== "") out[d.field] = String(d.value);
  }
  return out;
}

function parseLinesToResult(lines: string[], ocrText = ""): PassportOcrResult | null {
  const [repaired1, repaired2] = repairTd3Lines(lines[0], lines[1]);
  let parsed: ReturnType<typeof parseMrz> | null = null;
  try {
    parsed = parseMrz([repaired1, repaired2]);
  } catch {
    try {
      parsed = parseMrz(lines);
    } catch {
      return null;
    }
  }

  const fields = {
    ...fieldMap(parsed.details || []),
    ...(parsed.fields || {}),
  } as Record<string, string>;

  const natFromLine = repaired2.slice(10, 13).replace(/</g, "");
  const issFromLine = repaired1.slice(2, 5).replace(/</g, "");
  const docFromLine = repaired2.slice(0, 9).replace(/</g, "");
  const dobFromLine = repaired2.slice(13, 19);
  const expFromLine = repaired2.slice(21, 27);
  const sexFromLine = repaired2[20];

  let names = splitGivenNames(fields.firstName || "");
  let lastName = (fields.lastName || "").replace(/</g, " ").trim();
  const passportNumber = (fields.documentNumber || docFromLine).replace(/</g, "").trim();

  if (!passportNumber || passportNumber.length < 5 || !lastName) return null;

  const visual = ocrText ? extractVisualExtras(ocrText) : { nameTokens: [] as string[] };
  const refined = refineNamesFromVisual(lastName, names.firstName, names.middleName, visual);
  lastName = refined.lastName;
  names = { firstName: refined.firstName, middleName: refined.middleName };

  const natCode = (fields.nationality || natFromLine || issFromLine).replace(/</g, "");
  const issuing = (fields.issuingState || issFromLine).replace(/</g, "");

  const dob = mrzDateToIso(fields.birthDate || dobFromLine, false);
  const exp = mrzDateToIso(fields.expirationDate || expFromLine, true);
  const gender = sexToGender(fields.sex || sexFromLine);

  // Never treat DOB (or expiry) as the passport issue date
  let issueDate = visual.passportIssueDate;
  if (issueDate && (issueDate === dob || issueDate === exp)) {
    issueDate = undefined;
  }
  if (issueDate && dob && issueDate < dob) {
    issueDate = undefined;
  }

  const docCheckOk = checkOk(repaired2.slice(0, 9), repaired2[9]);
  const dobCheckOk = checkOk(repaired2.slice(13, 19), repaired2[19]);
  const expCheckOk = checkOk(repaired2.slice(21, 27), repaired2[27]);
  const checks = [docCheckOk, dobCheckOk, expCheckOk].filter(Boolean).length;

  return {
    firstName: names.firstName,
    middleName: names.middleName,
    lastName,
    passportNumber,
    dateOfBirth: dob,
    gender,
    nationality: alpha3ToCountryName(natCode) || alpha3ToCountryName(issuing),
    passportExpiryDate: exp,
    passportPlaceOfIssue: alpha3ToCountryName(issuing),
    placeOfBirth: visual.placeOfBirth,
    passportIssueDate: issueDate,
    passportType: "Normal",
    rawNationalityCode: natCode || undefined,
    confidence: parsed.valid || checks === 3 ? "high" : checks >= 2 ? "medium" : "low",
  };
}

type PrepVariant = { label: string; blob: Blob };

async function prepareImageVariants(file: File): Promise<PrepVariant[]> {
  const bitmap = await createImageBitmap(file);
  const variants: PrepVariant[] = [];

  const make = (
    label: string,
    sx: number,
    sy: number,
    sw: number,
    sh: number,
    scale: number,
    threshold: boolean
  ) => {
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.floor(sw * scale));
    canvas.height = Math.max(1, Math.floor(sh * scale));
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.fillStyle = "#fff";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(bitmap, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
    if (threshold) {
      const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const d = img.data;
      for (let i = 0; i < d.length; i += 4) {
        const g = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        const v = g > 145 ? 255 : 0;
        d[i] = d[i + 1] = d[i + 2] = v;
      }
      ctx.putImageData(img, 0, 0);
    }
    // push sync via toDataURL for reliability
    const dataUrl = canvas.toDataURL("image/png");
    const bin = atob(dataUrl.split(",")[1]);
    const arr = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
    variants.push({ label, blob: new Blob([arr], { type: "image/png" }) });
  };

  const w = bitmap.width;
  const h = bitmap.height;
  const aspect = w / Math.max(h, 1);
  const alreadyMrzStrip = aspect >= 2.2 || h < 280;

  const scale = Math.min(3, 1800 / w);

  // Full frame (important when user already cropped to MRZ)
  make("full", 0, 0, w, h, scale, true);
  make("full-gray", 0, 0, w, h, scale, false);

  if (!alreadyMrzStrip) {
    // Bottom band where MRZ lives on full passport page
    make("bottom", 0, Math.floor(h * 0.55), w, Math.floor(h * 0.45), scale, true);
    make("bottom-tight", 0, Math.floor(h * 0.7), w, Math.floor(h * 0.3), scale * 1.2, true);
  } else {
    // Slightly padded / re-threshold variants of the strip
    make("strip-hi", 0, 0, w, h, scale * 1.4, true);
  }

  bitmap.close();
  return variants;
}

async function ocrBlob(
  worker: Awaited<ReturnType<typeof createWorker>>,
  blob: Blob,
  psm: PSM,
  whitelist = true
): Promise<string> {
  await worker.setParameters({
    tessedit_char_whitelist: whitelist
      ? MRZ_CHARS
      : "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789<>/-. ",
    tessedit_pageseg_mode: psm,
  });
  const { data } = await worker.recognize(blob);
  return data.text || "";
}

/** Exposed for tests / debugging — parse raw OCR text into passport fields. */
export function parsePassportOcrText(ocrText: string): PassportOcrResult | null {
  const lines = extractMrzLines(ocrText);
  if (!lines) return null;
  return parseLinesToResult(lines, ocrText);
}

export async function scanPassportImage(
  file: File,
  onProgress?: (progress: { message: string; percent: number }) => void
): Promise<PassportOcrResult> {
  let lastPercent = 0;
  const report = (message: string, percent: number) => {
    // Never let the bar jump backwards — each recognize() otherwise resets 0→100%.
    lastPercent = Math.max(lastPercent, Math.min(100, Math.round(percent)));
    onProgress?.({ message, percent: lastPercent });
  };

  report("Preparing image…", 3);
  const variants = await prepareImageVariants(file);

  report("Loading OCR engine…", 8);
  let attemptIndex = 0;
  const psms = [PSM.SINGLE_BLOCK, PSM.SINGLE_COLUMN, PSM.SPARSE_TEXT];

  // Prefer MRZ-focused crops first
  const ordered = [...variants].sort((a, b) => {
    const score = (l: string) => (l.startsWith("bottom") ? 0 : l.startsWith("strip") ? 1 : 2);
    return score(a.label) - score(b.label);
  });

  const plannedAttempts = Math.max(1, ordered.length * psms.length);
  const mrzStart = 8;
  const mrzEnd = 88;

  const worker = await createWorker("eng", 1, {
    logger: (m) => {
      if (m.status === "recognizing text" && typeof m.progress === "number") {
        const slice = (mrzEnd - mrzStart) / plannedAttempts;
        const overall = mrzStart + attemptIndex * slice + Math.max(0, Math.min(1, m.progress)) * slice;
        report("Reading passport…", Math.min(mrzEnd - 1, overall));
      }
    },
  });

  try {
    let best: PassportOcrResult | null = null;
    let bestOcrText = "";

    outer: for (const variant of ordered) {
      for (const psm of psms) {
        report(`Scanning ${variant.label}…`, mrzStart + (attemptIndex / plannedAttempts) * (mrzEnd - mrzStart));
        const text = await ocrBlob(worker, variant.blob, psm, true);
        attemptIndex += 1;
        report(
          `Scanning ${variant.label}…`,
          mrzStart + (attemptIndex / plannedAttempts) * (mrzEnd - mrzStart)
        );

        const lines = extractMrzLines(text);
        if (!lines) continue;
        const result = parseLinesToResult(lines, text);
        if (!result) continue;
        if (!best) {
          best = result;
          bestOcrText = text;
        } else if (result.confidence === "high" && best.confidence !== "high") {
          best = result;
          bestOcrText = text;
        } else if (
          result.confidence === best.confidence &&
          result.passportNumber.length >= best.passportNumber.length &&
          /[AEIOU]/.test(result.lastName)
        ) {
          best = result;
          bestOcrText = text;
        }
        if (best.confidence === "high" && best.gender && best.dateOfBirth && best.passportExpiryDate) {
          break outer;
        }
      }
    }

    if (!best) {
      throw new Error(
        "Could not read the passport MRZ. Upload a clear photo of the biodata page with the two bottom lines fully visible."
      );
    }

    if (best.confidence === "low") {
      throw new Error(
        `Passport scan looks unreliable (got “${best.lastName}” / “${best.passportNumber}”). Retake a sharper, flatter photo of the biodata page and try again.`
      );
    }

    report("MRZ matched", 90);

    // Visual-zone pass (place of birth, issue date, name confirmation)
    const fullVariant = variants.find((v) => v.label === "full-gray") || variants[0];
    if (fullVariant) {
      report("Reading visual fields…", 92);
      try {
        const visualText = await ocrBlob(worker, fullVariant.blob, PSM.SPARSE_TEXT, false);
        const mergedText = `${bestOcrText}\n${visualText}`;
        const lines = extractMrzLines(bestOcrText) || extractMrzLines(mergedText);
        if (lines) {
          const refined = parseLinesToResult(lines, mergedText);
          if (refined && refined.confidence !== "low") {
            best = {
              ...refined,
              // Keep stronger MRZ identity fields if visual pass weakened them
              passportNumber: best.passportNumber,
              dateOfBirth: best.dateOfBirth || refined.dateOfBirth,
              passportExpiryDate: best.passportExpiryDate || refined.passportExpiryDate,
              gender: best.gender || refined.gender,
              nationality: best.nationality || refined.nationality,
              placeOfBirth: refined.placeOfBirth || best.placeOfBirth,
              passportIssueDate: refined.passportIssueDate || best.passportIssueDate,
              firstName: refined.firstName || best.firstName,
              middleName: refined.middleName || best.middleName,
              lastName: refined.lastName || best.lastName,
              confidence: best.confidence,
            };
          }
        }
      } catch {
        // visual pass is best-effort
      }
    }

    report("Passport data verified", 100);
    return best;
  } finally {
    await worker.terminate();
  }
}
