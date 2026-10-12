import assert from "node:assert/strict";
import { test } from "node:test";
import { resolveStageFromSlugOrId, slugifyStageName, stageHref } from "./stage-slug";
import type { WorkflowStage } from "@/types/workflow";

function stage(name: string, id = name.toLowerCase().replace(/\W/g, "")): WorkflowStage {
  return {
    id,
    name,
    sortOrder: 1,
    stageType: 0,
    isInitialStage: false,
    isFinalStage: false,
    statuses: [],
    parallelTracks: [],
  } as WorkflowStage;
}

test("the built-in boards keep the addresses people have bookmarked", () => {
  const stages = [stage("Ticket"), stage("Departure"), stage("Arrival"), stage("LMIS")];

  assert.equal(resolveStageFromSlugOrId(stages, "tickets")?.name, "Ticket");
  assert.equal(resolveStageFromSlugOrId(stages, "departures")?.name, "Departure");
  assert.equal(resolveStageFromSlugOrId(stages, "arrivals")?.name, "Arrival");
  assert.equal(resolveStageFromSlugOrId(stages, "lmis")?.name, "LMIS");
});

test("a stage an agency added is reachable by its own name", () => {
  const stages = [stage("Final Review")];

  assert.equal(slugifyStageName("Final Review"), "final-review");
  assert.equal(stageHref(stages, stages[0]), "/workflow/final-review");
  assert.equal(resolveStageFromSlugOrId(stages, "final-review")?.name, "Final Review");
});

test("a name that would open somebody else's board falls back to the id", () => {
  // Two stages that slug identically: the first wins the readable address, and the second must
  // not be given a link that opens the first.
  const a = stage("Pre screening", "aaa");
  const b = stage("Pre-screening", "bbb");
  const stages = [a, b];

  assert.equal(stageHref(stages, a), "/workflow/pre-screening");
  assert.equal(stageHref(stages, b), "/workflow/bbb");
  assert.equal(resolveStageFromSlugOrId(stages, "bbb")?.id, "bbb");
});

test("a name a URL cannot carry falls back to the id", () => {
  const odd = stage("؟؟؟", "ccc");
  assert.equal(slugifyStageName("؟؟؟"), "");
  assert.equal(stageHref([odd], odd), "/workflow/ccc");
});

test("a name that collides with a built-in slug falls back to the id", () => {
  // "Tickets" slugs to "tickets", which the table above answers with the stage named "Ticket".
  const ticket = stage("Ticket", "real-ticket");
  const impostor = stage("Tickets", "ddd");
  assert.equal(stageHref([ticket, impostor], impostor), "/workflow/ddd");
});
