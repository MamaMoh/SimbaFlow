import assert from "node:assert/strict";
import { test } from "node:test";
import { navigation, navigationForStages, type NavItem } from "./nav-items";
import type { WorkflowStage } from "@/types/workflow";

function stage(name: string, sortOrder: number): WorkflowStage {
  return {
    id: name.toLowerCase().replace(/\W/g, ""),
    name,
    sortOrder,
    stageType: 0,
    isInitialStage: false,
    isFinalStage: false,
    statuses: [],
    parallelTracks: [],
  } as WorkflowStage;
}

/** The pipeline as it is seeded, which is what every agency starts from. */
const DEFAULT_PIPELINE = [
  stage("Intake", 1),
  stage("New Contracts", 2),
  stage("Embassy", 3),
  stage("Case Executive", 4),
  stage("LMIS", 5),
  stage("Ticket", 6),
  stage("Departure", 7),
  stage("Arrival", 8),
  stage("Commission", 9),
];

/** The Workflow section's links, in order. */
function workflowLinks(items: NavItem[]): string[] {
  const start = items.findIndex((i) => i.sectionLabel === "Workflow");
  const names: string[] = [];
  for (let i = start + 1; i < items.length && !items[i].isSeparator; i++)
    names.push(items[i].name);
  return names;
}

test("the default pipeline produces the sidebar the app has always had", () => {
  assert.deepEqual(
    workflowLinks(navigationForStages(DEFAULT_PIPELINE)),
    workflowLinks(navigation),
  );
});

test("a stage an agency adds gets a link, in its own position", () => {
  const withScreening = [...DEFAULT_PIPELINE, stage("Screening", 2.5)];

  const links = workflowLinks(navigationForStages(withScreening));

  assert.ok(links.includes("Screening"));
  assert.equal(links.indexOf("Screening"), links.indexOf("New Contracts") + 1);
  assert.ok(links.indexOf("Screening") < links.indexOf("Embassy"));
});

test("a stage an agency removes takes its link with it", () => {
  const withoutCaseExecutive = DEFAULT_PIPELINE.filter((s) => s.name !== "Case Executive");

  const links = workflowLinks(navigationForStages(withoutCaseExecutive));

  assert.ok(!links.includes("Case Executive"));
  assert.ok(links.includes("Embassy"));
});

test("reordering the pipeline reorders the sidebar", () => {
  const swapped = DEFAULT_PIPELINE.map((s) =>
    s.name === "LMIS" ? stage("LMIS", 3.5) : s,
  );

  const links = workflowLinks(navigationForStages(swapped));

  assert.ok(links.indexOf("LMIS") < links.indexOf("Case Executive"));
});

test("Candidates stays at the top and Exceptions at the bottom", () => {
  // Neither is a stage: one is the way into the pipeline, the other is every stage at once.
  const links = workflowLinks(navigationForStages(DEFAULT_PIPELINE));

  assert.equal(links[0], "Candidates");
  assert.equal(links[links.length - 1], "Exceptions");
});

test("Intake does not get a second link to the candidate list", () => {
  assert.ok(!workflowLinks(navigationForStages(DEFAULT_PIPELINE)).includes("Intake"));
});

test("Commission keeps its one link, under Finance", () => {
  const items = navigationForStages(DEFAULT_PIPELINE);

  assert.ok(!workflowLinks(items).includes("Commissions"));
  assert.equal(items.filter((i) => i.name === "Commissions").length, 1);
});

test("nothing loaded yet leaves the sidebar exactly as it was", () => {
  // A sidebar that empties itself while a request is in flight is worse than one briefly stale.
  assert.deepEqual(workflowLinks(navigationForStages([])), workflowLinks(navigation));
});

test("a renamed built-in board becomes a general-purpose one", () => {
  // The bespoke Embassy page finds its candidates by the stage's name, so renaming the stage
  // leaves that page with nothing to show. The sidebar points at the board that still works
  // rather than at the one that no longer does.
  const renamed = DEFAULT_PIPELINE.map((s) =>
    s.name === "Embassy" ? stage("Saudi Embassy", 3) : s,
  );

  const items = navigationForStages(renamed);
  const embassy = items.find((i) => i.name === "Saudi Embassy");

  assert.ok(embassy);
  assert.equal(embassy.href, "/workflow/saudi-embassy");
  assert.ok(!workflowLinks(items).includes("Embassy"));
});
