/**
 * How long a placement runs for.
 *
 * It is a number of years, recorded the way the years of experience are. The field used to take
 * free text and collected "2 Years", "2 years", "2yrs" and "Two years" for the same contract —
 * nothing could count the column and the CV read differently depending on who typed it.
 *
 * Answers saved before that are not rewritten: a number box shows the number it starts with, and
 * anything with words in it keeps its own wording on the documents until somebody changes it.
 */

/** "2 Years" → "2", for a number input. Empty when there is no number to show. */
export function contractYears(stored: string | null | undefined): string {
  const found = /^\s*(\d+)/.exec(stored ?? "");
  return found ? found[1] : "";
}

/** What a page prints: "2 Years" from "2", and anything already in words as written. */
export function contractPeriodLabel(stored: string | null | undefined): string {
  const text = (stored ?? "").trim();
  if (!text) return "";
  if (/[a-z]/i.test(text)) return text;
  const years = Number(contractYears(text));
  if (!years) return text;
  return years === 1 ? "1 Year" : `${years} Years`;
}
