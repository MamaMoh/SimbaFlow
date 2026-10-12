import assert from "node:assert/strict";
import { test } from "node:test";
import { conditionFields, describeConditions, humaniseField } from "./condition-fields";
import type { WorkflowStage } from "@/types/workflow";

function stage(
  name: string,
  tracks: Record<string, string[]> = {},
  parallel: string[] = [],
): WorkflowStage {
  return {
    id: name.toLowerCase(),
    name,
    sortOrder: 1,
    stageType: parallel.length ? 1 : 0,
    isInitialStage: false,
    isFinalStage: false,
    parallelTracks: parallel.map((trackName, i) => ({
      id: `${name}-${trackName}`,
      trackName,
      completionStatus: "",
      sortOrder: i,
    })),
    statuses: Object.entries(tracks).flatMap(([trackName, values]) =>
      values.map((v, i) => ({
        id: `${name}-${trackName}-${v}`,
        name: v,
        sortOrder: i,
        isTerminal: false,
        trackName,
      })),
    ),
  } as WorkflowStage;
}

const PIPELINE = [
  stage("Embassy", { medical: ["Pending", "Booked", "Fit", "Unfit"], visa: ["Ready"] }, [
    "medical",
    "visa",
  ]),
  stage("LMIS", { milestone: ["Uploaded", "Issued"] }),
];

test("the fields on offer are the ones the engine can actually answer", () => {
  const fields = conditionFields(PIPELINE, PIPELINE[0]);
  const names = fields.map((f) => f.value);

  // Tracks, read from the agency's own steps.
  assert.ok(names.includes("medical"));
  assert.ok(names.includes("milestone"));
  // And the candidate's own record.
  assert.ok(names.includes("passportNumber"));
  // Nothing invented.
  assert.ok(!names.includes("passport"));
});

test("the step's own tracks come first, because that is what a rule on it usually means", () => {
  const fields = conditionFields(PIPELINE, PIPELINE[0]);

  assert.equal(fields.find((f) => f.value === "medical")?.group, "This step");
  assert.equal(fields.find((f) => f.value === "milestone")?.group, "Other steps");
  assert.equal(fields.find((f) => f.value === "passportNumber")?.group, "The candidate");
  assert.equal(fields[0].group, "This step");
});

test("a track carries the values it can hold, so the value is picked too", () => {
  const medical = conditionFields(PIPELINE, PIPELINE[0]).find((f) => f.value === "medical");

  assert.deepEqual(medical?.options, ["Booked", "Fit", "Pending", "Unfit"]);
});

test("a parallel track with no statuses of its own is still offered", () => {
  // It is declared on the stage; the agency simply has not listed its values. Leaving it out
  // would mean the one track an agency added by hand is the one they cannot write a rule about.
  const custom = [stage("Vetting", {}, ["police_check"])];

  const names = conditionFields(custom, custom[0]).map((f) => f.value);
  assert.ok(names.includes("police_check"));
});

test("field names read as English", () => {
  assert.equal(humaniseField("medical"), "Medical");
  assert.equal(humaniseField("departure_status"), "Departure status");
  assert.equal(humaniseField("passportNumber"), "Passport number");
});

test("a set of rules reads back as a sentence", () => {
  assert.equal(describeConditions(null), "Always");
  assert.equal(describeConditions({ operator: "AND", rules: [] }), "Always");

  assert.equal(
    describeConditions({
      operator: "AND",
      rules: [
        { field: "medical", op: "eq", value: "Fit" },
        { field: "tasheer", op: "eq", value: "Book Done" },
      ],
    }),
    "Medical is Fit and Tasheer is Book Done",
  );

  assert.equal(
    describeConditions({
      operator: "OR",
      rules: [
        { field: "visa", op: "eq", value: "Ready" },
        { field: "visa", op: "eq", value: "Submitted" },
      ],
    }),
    "Visa is Ready or Visa is Submitted",
  );

  assert.equal(
    describeConditions({
      operator: "AND",
      rules: [
        { field: "passportNumber", op: "not_empty" },
        { field: "labourId", op: "empty" },
        { field: "medical", op: "in", value: ["Fit", "Unfit"] },
      ],
    }),
    "Passport number is filled in and Labour ID is empty and Medical is Fit or Unfit",
  );
});
