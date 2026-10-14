import assert from "node:assert/strict";
import { test } from "node:test";
import {
  EPRO_CANDIDATE,
  INSURANCE_MEMBER,
  VISA_APPLICATION,
  formsWithStandingAnswers,
  standingAnswers,
} from "./manifest";
import { readFromText } from "./read";
import { asLines, displayDate, fillPlan, phoneSpellings, titleFor } from "./write";

/** Invented, like everything else in these tests. */
const CANDIDATE = {
  firstName: "HIWOT",
  middleName: "TADESSE",
  lastName: "GEBRU",
  gender: "Female",
  maritalStatus: "Married",
  dateOfBirth: "1994-05-12",
  passportNumber: "EQ0000001",
  labourId: "LAB0000123",
  phoneNumber: "+251911000000",
  email: "hiwot.example@example.com",
  woreda: "07",
  subcity: "Kombolcha",
  region: "Amhara",
  country: "Ethiopia",
  countryOfTravel: "Saudi Arabia",
};

const stepFor = (label: string) =>
  fillPlan(CANDIDATE, INSURANCE_MEMBER).steps.find((s) => s.label === label);

test("the plan follows the form, box by box", () => {
  const plan = fillPlan(CANDIDATE, INSURANCE_MEMBER);
  assert.deepEqual(
    plan.steps.map((s) => s.label),
    [
      "Title",
      "First Name",
      "Father Name",
      "Grand Father Name",
      "Gender",
      "Date Of Birth",
      "Passport Number",
      "Labor ID",
      "Phone",
      "Email",
      "Woreda",
      "Sub city",
      "Region",
      "Country",
      "Country Of Destination",
    ],
  );
});

test("the three name boxes are given, father, grandfather", () => {
  assert.equal(stepFor("First Name")?.value, "HIWOT");
  assert.equal(stepFor("Father Name")?.value, "TADESSE");
  assert.equal(stepFor("Grand Father Name")?.value, "GEBRU");
});

test("a title is worked out, since the form offers no blank", () => {
  assert.equal(titleFor({ gender: "Female", maritalStatus: "Married" }), "Mrs.");
  assert.equal(titleFor({ gender: "Female", maritalStatus: "Single" }), "Ms.");
  assert.equal(titleFor({ gender: "Male" }), "Mr.");
  // "Married" starts with the same letter as "Male" and is not a gender.
  assert.equal(titleFor({ gender: "", maritalStatus: "Married" }), "Mrs.");
});

test("a date carries both the value a date box takes and the one a person reads", () => {
  // A native date input only accepts ISO, however it chooses to display itself — so the two are
  // carried separately rather than the plan guessing which kind of box it is about to meet.
  const step = stepFor("Date Of Birth");
  assert.equal(step?.value, "1994-05-12");
  assert.equal(step?.display, "05/12/1994");
  assert.equal(displayDate("1994-05-12", "dmy"), "12/05/1994");
});

test("a phone goes out in both spellings, because forms disagree", () => {
  const step = stepFor("Phone");
  assert.equal(step?.value, "+251911000000");
  assert.equal(step?.display, "0911000000");
  assert.ok(step?.alternatives.includes("0911000000"));
  assert.ok(step?.alternatives.includes("+251911000000"));
  assert.deepEqual(phoneSpellings("0911000000").value, "+251911000000");
});

test("a dropdown is offered every spelling of the answer", () => {
  // You cannot paste into a select, and the option may be called something else. This is the
  // reason the extension earns its keep where a column of copy buttons cannot.
  const step = stepFor("Country Of Destination");
  assert.equal(step?.kind, "select");
  assert.ok(step?.alternatives.includes("Saudi Arabia"));
  assert.ok(step?.alternatives.includes("KSA"));
  assert.ok(stepFor("Gender")?.alternatives.includes("F"));
});

test("a box the form fills sensibly is left alone", () => {
  assert.equal(stepFor("Country")?.keepTheirs, true);
  assert.equal(stepFor("Labor ID")?.keepTheirs, false);
});

test("what the candidate cannot supply is named, not silently skipped", () => {
  const { steps, missing } = fillPlan({ ...CANDIDATE, labourId: "" }, INSURANCE_MEMBER);
  assert.ok(!steps.some((s) => s.label === "Labor ID"));
  assert.deepEqual(
    missing.map((m) => m.label),
    ["Labor ID"],
  );
});

test("the plan banks a row and never files the form", () => {
  // The extension fills and a person presses the button. Taking the last click too would buy
  // nothing and would mean a wrong row filed under a real person's name unlooked at.
  const plan = fillPlan(CANDIDATE, INSURANCE_MEMBER);
  assert.equal(plan.repeatButton, "Add Member");
  assert.ok(!plan.steps.some((s) => /submit|save|send/i.test(s.label)));
});

test("the plan reads back in tab order for anyone without the extension", () => {
  const lines = asLines(fillPlan(CANDIDATE, INSURANCE_MEMBER));
  assert.equal(lines[1], "First Name: HIWOT");
  assert.equal(lines[7], "Labor ID: LAB0000123");
});

test("a page read out of EPRO fills the insurance form it has never heard of", () => {
  // The point of one vocabulary for both directions: nothing in between translates, and a field
  // added to the map is learnt by the reader and the filler at the same time.
  const page = [
    "Surname:",
    "GEBRU",
    "Given Names:",
    "HIWOT TADESSE",
    "Gender:",
    "Female",
    "Date of Birth:",
    "1994-05-12",
    "Passport Number:",
    "EQ0000001",
  ].join("\n");

  const { values } = readFromText(page, EPRO_CANDIDATE);
  const plan = fillPlan(values, INSURANCE_MEMBER);
  const at = (label: string) => plan.steps.find((s) => s.label === label)?.value;

  assert.equal(at("First Name"), "HIWOT");
  assert.equal(at("Father Name"), "TADESSE");
  assert.equal(at("Grand Father Name"), "GEBRU");
  assert.equal(at("Passport Number"), "EQ0000001");
  assert.equal(at("Date Of Birth"), "1994-05-12");

  // EPRO does not hold these; the insurance form still wants them, and says so.
  assert.deepEqual(
    plan.missing.map((m) => m.label),
    ["Labor ID", "Phone", "Email", "Woreda", "Sub city", "Region", "Country", "Country Of Destination"],
  );
});

/** Invented, as above. A full enough record to fill a visa application. */
const FOR_VISA = {
  ...CANDIDATE,
  nationality: "Ethiopia",
  placeOfBirth: "Dessie",
  nationalId: "ID0000001",
  religion: "Muslim",
  occupation: "House Maid",
  qualification: "house maid",
  passportIssueDate: "2024-02-01",
  passportExpiryDate: "2034-01-31",
  sponsorName: "SALIM ALFAISAL",
  sponsorIdNumber: "2000000001",
  sponsorAddress: "Al Nuzha, Riyadh",
  sponsorPhone: "966500000000",
};

const visaStep = (label: string) =>
  fillPlan(FOR_VISA, VISA_APPLICATION).steps.find((s) => s.label === label);

test("the agency's standing answers fill the boxes that are not about the candidate", () => {
  // A third of this form is the same on every application the agency files. Storing it per
  // candidate would be forty thousand copies of "Addis Ababa".
  assert.equal(visaStep("Nearest Saudi Mission")?.value, "Addis Ababa");
  assert.equal(visaStep("Number of Entries")?.value, "Single");
  assert.equal(visaStep("Visa Validity")?.value, "90 Days");
  assert.equal(visaStep("Duration Of Stay")?.value, "90");
  assert.equal(visaStep("Transport Mode")?.value, "Air");
  assert.equal(visaStep("Purpose")?.value, "Work");
  assert.equal(visaStep("Source of Degree")?.value, "none");
});

test("the candidate beats the agency's usual answer", () => {
  // The one that matters: a passport that is not a Normal one must not have "Normal" put back
  // over it because that is what the agency files most of the time.
  const plan = fillPlan({ ...FOR_VISA, passportType: "Service" }, VISA_APPLICATION);
  assert.equal(plan.steps.find((s) => s.label === "Passport Type")?.value, "Service");
  assert.equal(visaStep("Passport Type")?.value, "Normal");
});

test("the Arabic name is reported as ours to supply, not quietly skipped", () => {
  // Transliterating an Ethiopian name into Arabic automatically would put a spelling on a visa
  // that does not match the passport at the border.
  const { missing, steps } = fillPlan(FOR_VISA, VISA_APPLICATION);
  const labels = missing.map((m) => m.label);
  assert.ok(labels.includes("Arabic First Name"));
  assert.ok(labels.includes("Arabic Family Name"));
  assert.ok(!steps.some((s) => s.label.startsWith("Arabic")));
});

test("the declaration is not in the plan at all", () => {
  // "I hereby declare that the information provided is true" is a person's attestation on a
  // government application. Nothing here ticks it, and nothing here answers a question about
  // the candidate's history that we do not hold.
  const labels = VISA_APPLICATION.fields.map((f) => f.label.toLowerCase());
  assert.ok(!labels.some((l) => l.includes("declare")));
  assert.ok(!labels.some((l) => l.includes("privacy policy")));
  assert.ok(!labels.some((l) => l.includes("previous nationality")));
  assert.ok(!labels.some((l) => l.includes("traveled to other countries")));
});

test("the occupation we register is offered under the name the visa form uses", () => {
  const step = visaStep("Occupation");
  assert.equal(step?.kind, "select");
  assert.ok(step?.alternatives.includes("House Worker"));
  assert.ok(step?.alternatives.includes("House Maid"));
});

test("the house the candidate is going to is the sponsor's", () => {
  assert.equal(visaStep("KSA Home Address")?.value, "Al Nuzha, Riyadh");
  assert.equal(visaStep("Sponsor Address")?.value, "Al Nuzha, Riyadh");
  assert.equal(visaStep("Phone Number")?.value, "+966500000000");
});

test("the three Latin name boxes follow the form's own order", () => {
  const plan = fillPlan(FOR_VISA, VISA_APPLICATION);
  const order = plan.steps.map((s) => s.label);
  assert.ok(order.indexOf("First Name") < order.indexOf("Second/Father Name"));
  assert.ok(order.indexOf("Second/Father Name") < order.indexOf("Other/G.Father Name"));
  assert.equal(visaStep("First Name")?.value, "HIWOT");
  assert.equal(visaStep("Second/Father Name")?.value, "TADESSE");
  assert.equal(visaStep("Other/G.Father Name")?.value, "GEBRU");
});

test("a Saudi number is not an Ethiopian one", () => {
  // The candidate's phone is local and the sponsor's is not. Reading both through the local
  // convention left the employer's number on a visa application with no country code on it.
  assert.equal(phoneSpellings("966500000000").value, "+966500000000");
  assert.equal(phoneSpellings("+966500000000").value, "+966500000000");
  assert.equal(phoneSpellings("0911000000").value, "+251911000000");
  assert.equal(phoneSpellings("0911000000").display, "0911000000");
  assert.equal(phoneSpellings("00251911000000").value, "+251911000000");
  assert.equal(phoneSpellings("00251911000000").display, "0911000000");
});

test("the visa form's email is the office's, not the candidate's", () => {
  // The box has a tooltip saying so, and filling it from the candidate would put a housemaid's
  // gmail address on a consular application as the agency's contact.
  const withAgency = { ...FOR_VISA, agencyEmail: "office@example.com" };
  const plan = fillPlan(withAgency, VISA_APPLICATION);
  assert.equal(plan.steps.find((s) => s.label === "Email Address")?.value, "office@example.com");

  // And the candidate's own address is not quietly used instead when the agency has none.
  const without = fillPlan({ ...FOR_VISA, agencyEmail: "" }, VISA_APPLICATION);
  assert.ok(!without.steps.some((s) => s.label === "Email Address"));
  assert.ok(without.missing.some((m) => m.label === "Email Address"));
});

test("the family name is the grandfather's, in both boxes that ask for it", () => {
  // The form wants four names and an Ethiopian one has three.
  assert.equal(visaStep("Other/G.Father Name")?.value, "GEBRU");
  assert.equal(visaStep("Last/Family Name")?.value, "GEBRU");
});

test("an agency that files differently gets its own answer", () => {
  const plan = fillPlan(FOR_VISA, VISA_APPLICATION, {
    "Nearest Saudi Mission": "Jeddah",
    "Visa Validity": "60 Days",
  });
  const at = (label: string) => plan.steps.find((s) => s.label === label)?.value;
  assert.equal(at("Nearest Saudi Mission"), "Jeddah");
  assert.equal(at("Visa Validity"), "60 Days");
  // Untouched boxes keep what the map ships.
  assert.equal(at("Number of Entries"), "Single");
});

test("an agency that has never opened the settings screen still files correctly", () => {
  // The whole point of the map shipping a default: an empty answers table changes nothing.
  const withNone = fillPlan(FOR_VISA, VISA_APPLICATION);
  const withBlanks = fillPlan(FOR_VISA, VISA_APPLICATION, {
    "Nearest Saudi Mission": "   ",
    "Purpose": "",
  });
  assert.deepEqual(
    withBlanks.steps.map((s) => s.value),
    withNone.steps.map((s) => s.value),
  );
});

test("the candidate still beats the agency's answer", () => {
  const plan = fillPlan({ ...FOR_VISA, passportType: "Service" }, VISA_APPLICATION, {
    "Passport Type": "Normal",
  });
  assert.equal(plan.steps.find((s) => s.label === "Passport Type")?.value, "Service");
});

test("the settings screen is offered exactly the boxes that are the agency's to answer", () => {
  const labels = standingAnswers(VISA_APPLICATION).map((f) => f.label);
  assert.ok(labels.includes("Nearest Saudi Mission"));
  assert.ok(labels.includes("Transport Mode"));
  // Candidate data is not the agency's standing answer, however often it repeats.
  assert.ok(!labels.includes("First Name"));
  assert.ok(!labels.includes("Passport Number"));
  // And a form with no standing answers is not offered an empty section.
  assert.equal(standingAnswers(INSURANCE_MEMBER).length, 0);
  assert.deepEqual(
    formsWithStandingAnswers().map((f) => f.site.id),
    ["visa-application"],
  );
});
