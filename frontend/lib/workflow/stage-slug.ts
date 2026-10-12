import type { WorkflowStage } from "@/types/workflow";

/**
 * Readable URLs for the boards that shipped with the app.
 *
 * The board a link points at is a stage, and a stage is identified by a GUID — but
 * /workflow/9f2c8a71-… in the address bar tells nobody anything, and these seven addresses are
 * in people's bookmarks and in every "have a look at" message ever sent. The plurals are the
 * ones that differ from the stage's own name: the stage is "Ticket", the board has always been
 * "Tickets".
 */
const STAGE_SLUG_TO_NAMES: Record<string, string[]> = {
  "new-contracts": ["New Contracts"],
  embassy: ["Embassy"],
  "case-executive": ["Case Executive"],
  lmis: ["LMIS"],
  tickets: ["Ticket", "Tickets"],
  ticket: ["Ticket"],
  departures: ["Departure", "Departures"],
  departure: ["Departure"],
  arrivals: ["Arrival", "Arrivals"],
  arrival: ["Arrival"],
  commissions: ["Commission", "Commissions"],
  commission: ["Commission"],
  intake: ["Intake"],
};

/** The address segment a stage's own name would make. Empty when the name has nothing in it a URL can carry. */
export function slugifyStageName(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

/**
 * The stage a /workflow/… address refers to, by id or by the name in the address.
 *
 * Both, because the seven built-in boards are addressed by name and anything an agency adds is
 * addressed by whichever of the two survives the round trip — see stageHref.
 */
export function resolveStageFromSlugOrId(
  stages: WorkflowStage[],
  stageIdOrSlug: string,
): WorkflowStage | undefined {
  const byId = stages.find((s) => s.id === stageIdOrSlug);
  if (byId) return byId;

  const names = STAGE_SLUG_TO_NAMES[stageIdOrSlug.toLowerCase()] ?? [
    stageIdOrSlug.replace(/-/g, " "),
  ];

  return stages.find((s) =>
    names.some((n) => s.name.toLowerCase() === n.toLowerCase()),
  );
}

/**
 * Where a stage's board lives.
 *
 * A name makes the better address, so it is tried first — but only if asking for it back gives
 * this same stage. Two stages whose names slug the same way, a name that is only punctuation, a
 * name that collides with one of the built-in slugs above: in each of those the readable address
 * would open somebody else's board, and the id is the honest answer.
 */
export function stageHref(stages: WorkflowStage[], stage: WorkflowStage): string {
  const slug = slugifyStageName(stage.name);
  const resolvesBack = slug ? resolveStageFromSlugOrId(stages, slug)?.id === stage.id : false;
  return `/workflow/${resolvesBack ? slug : stage.id}`;
}
