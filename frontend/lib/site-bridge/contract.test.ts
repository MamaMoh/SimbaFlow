import assert from "node:assert/strict";
import { test } from "node:test";
import { contractEnd, saudiCityIn, termMonths } from "./contract";
import { LMIS_CONTRACT } from "./manifest";
import { fillPlan } from "./write";

test("a bare number is years, and words are taken at their word", () => {
  assert.equal(termMonths("2"), 24);
  assert.equal(termMonths("2 Years"), 24);
  assert.equal(termMonths("Two years"), 24);
  assert.equal(termMonths("24 months"), 24);
  // Nothing recorded is the two years the intake form fills in by default.
  assert.equal(termMonths(""), 24);
  assert.equal(termMonths(null), 24);
  // Something recorded that cannot be read is not guessed at.
  assert.equal(termMonths("until further notice"), null);
});

test("the contract ends the term later, on the same day of the month", () => {
  assert.equal(contractEnd("2026-03-01", "2"), "2028-03-01");
  assert.equal(contractEnd("2026-03-01T00:00:00Z", "18 months"), "2027-09-01");
  // There is no 29th of February in 2030.
  assert.equal(contractEnd("2028-02-29", "2"), "2030-02-28");
  assert.equal(contractEnd("", "2"), "");
  assert.equal(contractEnd("2026-03-01", "until further notice"), "");
});

test("the employer's city is read out of the address, in either script", () => {
  assert.equal(saudiCityIn("الرياض - حي النرجس"), "Riyadh");
  assert.equal(saudiCityIn("مكة المكرمة، العزيزية"), "Mecca");
  assert.equal(saudiCityIn("Al Khobar, Eastern Province"), "Khobar");
  assert.equal(saudiCityIn("خميس مشيط"), "Khamis Mushait");
  // Two cities is a guess, and so is none.
  assert.equal(saudiCityIn("الرياض طريق جدة"), "");
  assert.equal(saudiCityIn("حي النرجس"), "");
  assert.equal(saudiCityIn(""), "");
  // Not a city inside a longer word.
  assert.equal(saudiCityIn("Tabukiya Street"), "");
});

test("the LMIS contract plan fills what SimbaFlow holds and names the employer's two boxes it does not", () => {
  const plan = fillPlan(
    {
      contractDate: "2026-03-01",
      contractEndDate: "2028-03-01",
      passportNumber: "EQ0000001",
      visaNumber: "1234567890",
      sponsorCity: "Riyadh",
      sponsorName: "Invented Sponsor Name",
      sponsorPhone: "966500000000",
    },
    LMIS_CONTRACT,
  );

  const step = (label: string) => plan.steps.find((s) => s.label === label);
  assert.equal(step("Beginning of Contract")?.value, "2026-03-01");
  assert.equal(step("Ending of Contract")?.value, "2028-03-01");
  assert.equal(step("Visa Number")?.value, "1234567890");
  assert.equal(step("Employer Full Name")?.value, "Invented Sponsor Name");
  // A Saudi number keeps its country code; it is not an Ethiopian one.
  assert.equal(step("Employer Phone number")?.value, "+966500000000");
  assert.ok(step("Employer city")?.alternatives.includes("Ar Riyadh"));
  assert.deepEqual(
    plan.missing.map((m) => m.label),
    ["Alternative Phone Number", "House Number"],
  );
});
