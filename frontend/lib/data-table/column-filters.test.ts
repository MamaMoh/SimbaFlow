import { strict as assert } from "node:assert";
import test from "node:test";
import {
  defaultOperator,
  describe as describeFilter,
  filterFor,
  isComplete,
  needsSecondValue,
  needsValue,
  operatorLabel,
  operatorsFor,
  toQueryParam,
  withFilter,
  type ColumnFilter,
} from "./column-filters";

test("text columns lead with Contains, because that is what typing part of a name means", () => {
  assert.equal(defaultOperator("text"), "contains");
  assert.equal(operatorsFor("text")[0], "contains");
});

test("numbers and dates get the comparisons and text does not", () => {
  assert.ok(operatorsFor("number").includes("between"));
  assert.ok(operatorsFor("date").includes("gte"));
  assert.ok(!operatorsFor("text").includes("between"));
  assert.ok(!operatorsFor("text").includes("gt"));
});

test("a date reads as earlier and later, not greater and less", () => {
  assert.equal(operatorLabel("gte", "date"), "On or after");
  assert.equal(operatorLabel("gte", "number"), "Greater than or equal to");
});

test("blank and not blank need no value", () => {
  assert.equal(needsValue("blank"), false);
  assert.equal(needsValue("notblank"), false);
  assert.equal(needsValue("eq"), true);
  assert.equal(needsSecondValue("between"), true);
  assert.equal(needsSecondValue("gt"), false);
});

test("a half-typed filter is not finished", () => {
  assert.equal(isComplete({ field: "age", op: "gt" }), false);
  assert.equal(isComplete({ field: "age", op: "gt", value: "" }), false);
  assert.equal(isComplete({ field: "age", op: "between", value: "20" }), false);
  assert.equal(isComplete({ field: "age", op: "gt", value: "20" }), true);
  assert.equal(isComplete({ field: "labourId", op: "blank" }), true);
});

test("only the finished ones are asked of the server", () => {
  // Leaving the list alone while somebody is still typing, rather than emptying it and filling
  // it again on the next keystroke.
  const filters: ColumnFilter[] = [
    { field: "age", op: "gte", value: "25" },
    { field: "occupation", op: "contains", value: "" },
  ];
  const sent = JSON.parse(toQueryParam(filters)!);
  assert.equal(sent.length, 1);
  assert.deepEqual(sent[0], { field: "age", op: "gte", value: "25" });
});

test("nothing to ask sends no parameter at all", () => {
  assert.equal(toQueryParam([]), undefined);
  assert.equal(toQueryParam([{ field: "age", op: "gt" }]), undefined);
});

test("a value the operator has no box for does not travel with it", () => {
  const sent = JSON.parse(toQueryParam([{ field: "labourId", op: "blank", value: "leftover" }])!);
  assert.deepEqual(sent[0], { field: "labourId", op: "blank" });

  const range = JSON.parse(
    toQueryParam([{ field: "age", op: "gt", value: "20", value2: "40" }])!,
  );
  assert.deepEqual(range[0], { field: "age", op: "gt", value: "20" });
});

test("values are trimmed on the way out", () => {
  const sent = JSON.parse(toQueryParam([{ field: "occupation", op: "contains", value: "  maid " }])!);
  assert.equal(sent[0].value, "maid");
});

test("one filter per column: setting one replaces it rather than stacking", () => {
  let filters: ColumnFilter[] = [];
  filters = withFilter(filters, "age", { field: "age", op: "gt", value: "20" });
  filters = withFilter(filters, "age", { field: "age", op: "lt", value: "40" });
  assert.equal(filters.length, 1);
  assert.equal(filters[0].op, "lt");
});

test("clearing a column takes its filter out and leaves the others", () => {
  let filters: ColumnFilter[] = [
    { field: "age", op: "gt", value: "20" },
    { field: "occupation", op: "contains", value: "maid" },
  ];
  filters = withFilter(filters, "age", null);
  assert.equal(filters.length, 1);
  assert.equal(filterFor(filters, "age"), undefined);
  assert.equal(filterFor(filters, "occupation")?.value, "maid");
});

test("a set filter reads as a sentence on its funnel", () => {
  assert.equal(
    describeFilter({ field: "age", op: "gte", value: "25" }, "Age", "number"),
    "Age greater than or equal to 25",
  );
  assert.equal(
    describeFilter({ field: "age", op: "between", value: "20", value2: "30" }, "Age", "number"),
    "Age between 20 and 30",
  );
  assert.equal(
    describeFilter({ field: "labourId", op: "blank" }, "Labour ID", "text"),
    "Labour ID is blank",
  );
});
