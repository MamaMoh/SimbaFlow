import assert from "node:assert/strict";
import { test } from "node:test";
import { contractPeriodLabel, contractYears } from "./contract-period";

test("a number box shows the number an older free-text answer starts with", () => {
  assert.equal(contractYears("2"), "2");
  assert.equal(contractYears("2 Years"), "2");
  assert.equal(contractYears(" 3 years "), "3");
  assert.equal(contractYears("Two years"), "");
  assert.equal(contractYears(null), "");
});

test("a page prints the unit, and leaves an answer already in words alone", () => {
  assert.equal(contractPeriodLabel("2"), "2 Years");
  assert.equal(contractPeriodLabel("1"), "1 Year");
  assert.equal(contractPeriodLabel("24 months"), "24 months");
  assert.equal(contractPeriodLabel("Two years"), "Two years");
  assert.equal(contractPeriodLabel(""), "");
});
