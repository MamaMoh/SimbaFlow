import { strict as assert } from "node:assert";
import test from "node:test";
import {
  isArranged,
  layoutKey,
  moveOnto,
  moveWithin,
  resolveOrder,
  resolveVisibility,
} from "./table-layout";

const LOCKED = { start: ["select", "index"], end: ["actions"] };
const ALL = ["select", "index", "name", "passport", "age", "stage", "actions"];

test("with nothing saved, the board's own order is what is drawn", () => {
  assert.deepEqual(resolveOrder(ALL, undefined, LOCKED), ALL);
});

test("a saved arrangement is kept, between the columns that do not move", () => {
  const saved = ["stage", "name", "age", "passport"];
  assert.deepEqual(resolveOrder(ALL, saved, LOCKED), [
    "select",
    "index",
    "stage",
    "name",
    "age",
    "passport",
    "actions",
  ]);
});

test("a column that no longer exists is dropped from a saved arrangement", () => {
  // Someone arranged this board before a release removed a column.
  const saved = ["stage", "labourId", "name", "passport", "age"];
  assert.deepEqual(resolveOrder(ALL, saved, LOCKED), [
    "select",
    "index",
    "stage",
    "name",
    "passport",
    "age",
    "actions",
  ]);
});

test("a column added since arrives at the end rather than reshuffling the arrangement", () => {
  const saved = ["stage", "name"];
  const withNew = [...ALL.slice(0, -1), "partner", "actions"];
  assert.deepEqual(resolveOrder(withNew, saved, LOCKED), [
    "select",
    "index",
    "stage",
    "name",
    "passport",
    "age",
    "partner",
    "actions",
  ]);
});

test("a saved arrangement cannot drag the pinned column out of its place", () => {
  // Whatever is in storage — including something edited by hand — actions stays last and the
  // tick box stays first, because the table pins them there.
  const saved = ["actions", "stage", "select", "name"];
  const order = resolveOrder(ALL, saved, LOCKED);
  assert.equal(order[0], "select");
  assert.equal(order[1], "index");
  assert.equal(order[order.length - 1], "actions");
});

test("the same id twice in storage is only drawn once", () => {
  const order = resolveOrder(ALL, ["name", "name", "stage"], LOCKED);
  assert.equal(order.filter((id) => id === "name").length, 1);
});

test("moving a column one place to the left", () => {
  const order = resolveOrder(ALL, undefined, LOCKED);
  assert.deepEqual(moveWithin(order, "age", -1, LOCKED), [
    "select",
    "index",
    "name",
    "age",
    "passport",
    "stage",
    "actions",
  ]);
});

test("a column at the end of the movable run does not move past the pinned one", () => {
  const order = resolveOrder(ALL, undefined, LOCKED);
  assert.deepEqual(moveWithin(order, "stage", 1, LOCKED), order);
  assert.deepEqual(moveWithin(order, "name", -1, LOCKED), order);
});

test("a locked column cannot be moved at all", () => {
  const order = resolveOrder(ALL, undefined, LOCKED);
  assert.deepEqual(moveWithin(order, "actions", -1, LOCKED), order);
  assert.deepEqual(moveWithin(order, "select", 1, LOCKED), order);
});

test("dropping one heading onto another takes that place", () => {
  const order = resolveOrder(ALL, undefined, LOCKED);
  assert.deepEqual(moveOnto(order, "stage", "name", LOCKED), [
    "select",
    "index",
    "stage",
    "name",
    "passport",
    "age",
    "actions",
  ]);
});

test("dropping a heading on itself changes nothing", () => {
  const order = resolveOrder(ALL, undefined, LOCKED);
  assert.deepEqual(moveOnto(order, "name", "name", LOCKED), order);
});

test("the board's defaults hold until this person decides otherwise", () => {
  assert.deepEqual(
    resolveVisibility(ALL, { stage: false }, undefined),
    { stage: false },
  );
  assert.deepEqual(
    resolveVisibility(ALL, { stage: false }, { stage: true, age: false }),
    { stage: true, age: false },
  );
});

test("a saved choice about a column that is gone is ignored", () => {
  assert.deepEqual(resolveVisibility(ALL, {}, { labourId: false }), {});
});

test("a board nobody has touched offers nothing to reset", () => {
  assert.equal(isArranged(ALL, {}, null, LOCKED), false);
  assert.equal(isArranged(ALL, {}, {}, LOCKED), false);
  // Saved, but saying exactly what the board already does.
  assert.equal(
    isArranged(ALL, {}, { order: ["name", "passport", "age", "stage"], visibility: {} }, LOCKED),
    false,
  );
});

test("a board that has been arranged offers the way back", () => {
  assert.equal(isArranged(ALL, {}, { order: ["stage", "name"] }, LOCKED), true);
  assert.equal(isArranged(ALL, {}, { visibility: { age: false } }, LOCKED), true);
});

test("each board remembers under its own key", () => {
  assert.notEqual(layoutKey("candidates"), layoutKey("departures"));
  assert.match(layoutKey("candidates"), /candidates$/);
});
