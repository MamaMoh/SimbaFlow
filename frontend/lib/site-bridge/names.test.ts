import assert from "node:assert/strict";
import { test } from "node:test";
import { joinName, splitFullName, splitGivenNames } from "./names";

test("a whole name divides at its first and last word", () => {
  assert.deepEqual(splitFullName("Hiwot Tadesse Gebru"), {
    firstName: "Hiwot",
    middleName: "Tadesse",
    lastName: "Gebru",
  });
});

test("a long name keeps every middle part", () => {
  // Dropping one would be silent and would travel onto a visa application.
  assert.deepEqual(splitFullName("Hiwot Tadesse Bekele Gebru"), {
    firstName: "Hiwot",
    middleName: "Tadesse Bekele",
    lastName: "Gebru",
  });
});

test("two words are a first name and a father's name", () => {
  assert.deepEqual(splitFullName("Hiwot Gebru"), {
    firstName: "Hiwot",
    middleName: "",
    lastName: "Gebru",
  });
});

test("one word is a name, not an error", () => {
  assert.deepEqual(splitFullName("Hiwot"), {
    firstName: "Hiwot",
    middleName: "",
    lastName: "",
  });
  assert.deepEqual(splitFullName("   "), { firstName: "", middleName: "", lastName: "" });
});

test("given names divide differently, because the surname is already separate", () => {
  // A passport prints the surname on its own, so everything after the first given name is the
  // father's. Reading it with the whole-name rule hands the father's name back as a surname.
  assert.deepEqual(splitGivenNames("Hiwot Tadesse"), {
    firstName: "Hiwot",
    middleName: "Tadesse",
  });
  assert.deepEqual(splitGivenNames("Hiwot"), { firstName: "Hiwot", middleName: "" });
});

test("the parts go back together the way the box shows them", () => {
  assert.equal(joinName({ firstName: "Hiwot", middleName: "Tadesse", lastName: "Gebru" }), "Hiwot Tadesse Gebru");
  assert.equal(joinName({ firstName: "Hiwot", middleName: "", lastName: "Gebru" }), "Hiwot Gebru");
  assert.equal(joinName({ firstName: "Hiwot" }), "Hiwot");
  assert.equal(joinName({}), "");
});

test("a whole name survives the round trip", () => {
  for (const name of ["Hiwot Tadesse Gebru", "Hiwot Gebru", "Hiwot", "Meron Abate Bekele Desta"]) {
    assert.equal(joinName(splitFullName(name)), name);
  }
});
