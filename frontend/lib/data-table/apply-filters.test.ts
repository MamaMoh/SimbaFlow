import test from "node:test";
import assert from "node:assert/strict";
import { applyColumnFilters, kindsOf, matches } from "./apply-filters";
import type { ColumnFilter } from "./column-filters";

const rows = [
  { name: "Almaz Kebede", years: 6, joined: "2024-03-01", note: "" },
  { name: "Hanan Ahmed", years: 2, joined: "2025-01-15", note: null },
  { name: "Selam Bekele", years: null, joined: "2023-11-30", note: "urgent" },
];

const kinds = { name: "text", years: "number", joined: "date", note: "text" } as const;

const run = (filter: ColumnFilter) =>
  applyColumnFilters(rows, [filter], kinds as Record<string, "text" | "number" | "date">).map(
    (r) => r.name,
  );

test("text contains is case-insensitive", () => {
  assert.deepEqual(run({ field: "name", op: "contains", value: "kEbEdE" }), ["Almaz Kebede"]);
});

test("starts with matches the beginning only", () => {
  assert.deepEqual(run({ field: "name", op: "startswith", value: "Hanan" }), ["Hanan Ahmed"]);
  assert.deepEqual(run({ field: "name", op: "startswith", value: "Ahmed" }), []);
});

test("blank covers both an empty string and nothing recorded", () => {
  assert.deepEqual(run({ field: "note", op: "blank" }), ["Almaz Kebede", "Hanan Ahmed"]);
  assert.deepEqual(run({ field: "note", op: "notblank" }), ["Selam Bekele"]);
});

test("numbers compare as numbers, not as text", () => {
  assert.deepEqual(run({ field: "years", op: "gt", value: "2" }), ["Almaz Kebede"]);
  assert.deepEqual(run({ field: "years", op: "lte", value: "6" }), [
    "Almaz Kebede",
    "Hanan Ahmed",
  ]);
});

test("a row with nothing recorded answers no comparison", () => {
  // Including "not equal to", which would otherwise sweep every blank row in.
  assert.deepEqual(run({ field: "years", op: "neq", value: "6" }), ["Hanan Ahmed"]);
});

test("between takes its bounds either way round", () => {
  const up = run({ field: "years", op: "between", value: "2", value2: "6" });
  const down = run({ field: "years", op: "between", value: "6", value2: "2" });
  assert.deepEqual(up, ["Almaz Kebede", "Hanan Ahmed"]);
  assert.deepEqual(down, up);
});

test("dates compare by day", () => {
  assert.deepEqual(run({ field: "joined", op: "gte", value: "2024-01-01" }), [
    "Almaz Kebede",
    "Hanan Ahmed",
  ]);
  assert.deepEqual(run({ field: "joined", op: "eq", value: "2024-03-01" }), ["Almaz Kebede"]);
});

test("a timestamp still matches the day it falls on", () => {
  assert.equal(
    matches("2024-03-01T22:40:00Z", { field: "joined", op: "eq", value: "2024-03-01" }, "date"),
    true,
  );
});

test("a value that will not parse leaves the list alone", () => {
  assert.deepEqual(run({ field: "years", op: "gt", value: "banana" }), [
    "Almaz Kebede",
    "Hanan Ahmed",
    "Selam Bekele",
  ]);
});

test("a half-typed filter leaves the list alone", () => {
  assert.deepEqual(run({ field: "name", op: "contains", value: "" }), [
    "Almaz Kebede",
    "Hanan Ahmed",
    "Selam Bekele",
  ]);
  assert.deepEqual(run({ field: "years", op: "between", value: "2" }), [
    "Almaz Kebede",
    "Hanan Ahmed",
    "Selam Bekele",
  ]);
});

test("every filter has to be answered, not any", () => {
  const both = applyColumnFilters(
    rows,
    [
      { field: "name", op: "contains", value: "a" },
      { field: "years", op: "gte", value: "6" },
    ],
    kinds as Record<string, "text" | "number" | "date">,
  );
  assert.deepEqual(both.map((r) => r.name), ["Almaz Kebede"]);
});

test("a field the board never declared is read as text", () => {
  assert.deepEqual(
    applyColumnFilters(rows, [{ field: "name", op: "contains", value: "selam" }]).map(
      (r) => r.name,
    ),
    ["Selam Bekele"],
  );
});

test("kinds are read off the column definitions", () => {
  assert.deepEqual(
    kindsOf([
      { meta: { filter: { field: "years", kind: "number" } } },
      { meta: { filter: { field: "name", kind: "text" } } },
      { meta: undefined },
      {},
    ]),
    { years: "number", name: "text" },
  );
});

test("a reader of the board's own can reach a nested value", () => {
  const nested = [{ id: "1", candidate: { fullName: "Almaz Kebede" } }];
  const kept = applyColumnFilters(
    nested,
    [{ field: "fullName", op: "eq", value: "almaz kebede" }],
    { fullName: "text" },
    (row, field) => (field === "fullName" ? row.candidate.fullName : undefined),
  );
  assert.equal(kept.length, 1);
});
