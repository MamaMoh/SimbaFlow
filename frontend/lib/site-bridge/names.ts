/**
 * One name, three boxes, and the rule for going between them.
 *
 * SimbaFlow asks for a name in a single box, because that is how a passport prints it and how
 * the desk reads it aloud. Every system it talks to wants the parts separately — first, father,
 * grandfather, family — under four different headings per portal. So a name is split on the way
 * out and joined on the way in, and both have to use the same rule or a candidate registered
 * from a pasted page comes back with their father's name in the family box.
 *
 * Written down once here for that reason. The registration form, the paste parser and the form
 * filler all call these rather than each keeping its own idea of where a name divides.
 *
 * Ethiopian names are given, father, grandfather — no inherited surname — which is why the
 * middle of a name is as load-bearing as either end and never an initial to be dropped.
 */

export type NameParts = {
  firstName: string;
  middleName: string;
  lastName: string;
};

const words = (value: string) => String(value || "").trim().split(/\s+/).filter(Boolean);

/**
 * A whole name as one string, divided.
 *
 * First word, last word, and everything between as the middle — so a four-part name keeps both
 * of its middle names rather than losing one. A single word is a first name with nothing after
 * it, which is a real record and not an error.
 */
export function splitFullName(full: string): NameParts {
  const parts = words(full);
  if (parts.length === 0) return { firstName: "", middleName: "", lastName: "" };
  if (parts.length === 1) return { firstName: parts[0], middleName: "", lastName: "" };
  return {
    firstName: parts[0],
    middleName: parts.slice(1, -1).join(" "),
    lastName: parts[parts.length - 1],
  };
}

/**
 * A passport's given names, divided.
 *
 * Different from above, and deliberately: a passport prints the surname on its own line, so
 * everything in the given-names line after the first word is the father's name. Reading it with
 * the whole-name rule would hand the father's name back as a surname and leave the middle empty.
 */
export function splitGivenNames(given: string): Pick<NameParts, "firstName" | "middleName"> {
  const parts = words(given);
  return {
    firstName: parts[0] ?? "",
    middleName: parts.slice(1).join(" "),
  };
}

/** The three parts as the one box shows them. Blanks drop out rather than doubling the spaces. */
export function joinName(parts: Partial<NameParts>): string {
  return [parts.firstName, parts.middleName, parts.lastName].filter(Boolean).join(" ").trim();
}
