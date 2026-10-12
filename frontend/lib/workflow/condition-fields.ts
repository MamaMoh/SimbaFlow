import type { WorkflowStage } from "@/types/workflow";

/**
 * What a condition can be written about.
 *
 * A condition is checked against two things, and only these two: the tracks a candidate carries
 * — medical, tasheer, visa, and whatever else the agency's own steps declare — and a fixed set
 * of fields on the candidate's record. Anything else written in the field box silently reads as
 * empty, which makes "is not empty" always false and "equals" never true, and the button it
 * guards is simply never offered. Nobody ever finds out why.
 *
 * So the field is chosen from a list rather than typed. The tracks are read from the agency's
 * own stages, so they are always the ones that exist; the candidate fields are listed below,
 * and must match BuildCandidateFields in WorkflowEngineService.cs — there is a test over there
 * that points back here.
 */

export type FieldOption = {
  value: string;
  label: string;
  /** Where it comes from, so the picker can group them. */
  group: "This step" | "Other steps" | "The candidate";
  /** Known values, when the field is a track with declared statuses. */
  options?: string[];
};

/** The candidate's own record, as the workflow engine exposes it. */
export const CANDIDATE_FIELDS: { value: string; label: string }[] = [
  { value: "firstName", label: "First name" },
  { value: "lastName", label: "Last name" },
  { value: "passportNumber", label: "Passport number" },
  { value: "labourId", label: "Labour ID" },
  { value: "nationality", label: "Nationality" },
  { value: "phoneNumber", label: "Phone number" },
  { value: "email", label: "Email" },
  { value: "countryOfTravel", label: "Country of travel" },
  { value: "destination", label: "Destination" },
  { value: "partnerName", label: "Partner" },
  { value: "contractDate", label: "Contract date" },
  { value: "ticket_status", label: "Ticket status" },
  { value: "flight_date", label: "Flight date" },
];

/** "medical" → "Medical", "departure_status" → "Departure status". */
export function humaniseField(field: string): string {
  const known = CANDIDATE_FIELDS.find((f) => f.value === field);
  if (known) return known.label;
  const spaced = field.replace(/_/g, " ").trim();
  return spaced ? spaced[0].toUpperCase() + spaced.slice(1) : field;
}

/** Every track declared anywhere in the pipeline, with the values each one can hold. */
function tracks(stages: readonly WorkflowStage[]): Map<string, Set<string>> {
  const found = new Map<string, Set<string>>();
  const note = (track: string | undefined | null, value?: string) => {
    if (!track) return;
    const key = track.trim();
    if (!key) return;
    if (!found.has(key)) found.set(key, new Set());
    if (value) found.get(key)!.add(value);
  };

  for (const stage of stages) {
    for (const t of stage.parallelTracks ?? []) note(t.trackName);
    for (const s of stage.statuses ?? []) note(s.trackName, s.name);
  }
  return found;
}

/** The tracks a single step declares, which are the ones a condition on it usually means. */
function tracksOf(stage: WorkflowStage | undefined): Set<string> {
  const own = new Set<string>();
  if (!stage) return own;
  for (const t of stage.parallelTracks ?? []) if (t.trackName) own.add(t.trackName.trim());
  for (const s of stage.statuses ?? []) if (s.trackName) own.add(s.trackName.trim());
  return own;
}

/**
 * Everything a condition on this step could sensibly be about, nearest first.
 *
 * @param focus The step the condition is attached to — a transition's source, or a mirror's
 *   "from" board. Its own tracks come first, because those are what a rule on that board is
 *   almost always testing.
 */
export function conditionFields(
  stages: readonly WorkflowStage[],
  focus?: WorkflowStage,
): FieldOption[] {
  const all = tracks(stages);
  const near = tracksOf(focus);

  const fromTracks: FieldOption[] = [...all.entries()]
    .map(([track, values]) => ({
      value: track,
      label: humaniseField(track),
      group: (near.has(track) ? "This step" : "Other steps") as FieldOption["group"],
      options: [...values].sort(),
    }))
    .sort((a, b) => a.label.localeCompare(b.label));

  return [
    ...fromTracks.filter((f) => f.group === "This step"),
    ...fromTracks.filter((f) => f.group === "Other steps"),
    ...CANDIDATE_FIELDS.map((f) => ({ ...f, group: "The candidate" as const })),
  ];
}

/** A plain-English reading of a condition group, for the row that shows it in a table. */
export function describeConditions(group: unknown): string {
  if (!group || typeof group !== "object") return "Always";
  const g = group as { operator?: string; rules?: { field: string; op: string; value?: unknown }[] };
  if (!g.rules?.length) return "Always";

  const one = (r: { field: string; op: string; value?: unknown }) => {
    const field = humaniseField(r.field);
    const value = Array.isArray(r.value) ? r.value.join(" or ") : String(r.value ?? "");
    switch (r.op) {
      case "eq":
        return `${field} is ${value}`;
      case "neq":
        return `${field} is not ${value}`;
      case "in":
        return `${field} is ${value}`;
      case "not_empty":
        return `${field} is filled in`;
      case "empty":
        return `${field} is empty`;
      default:
        return `${field} ${r.op} ${value}`;
    }
  };

  return g.rules.map(one).join(g.operator === "OR" ? " or " : " and ");
}
