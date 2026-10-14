import assert from "node:assert/strict";
import { test } from "node:test";
import { EPRO_CANDIDATE, EPRO_SPONSOR, type SiteManifest } from "./manifest";
import { readFromText, tidyPhone, toIsoDate } from "./read";

/**
 * Every value in this file is invented. The pages these maps were written against hold real
 * candidates, real employers and real passport numbers, and none of that belongs in a repository
 * — least of all one that is public.
 */
const EPRO_PAGE = `
Personal Information
Surname:
GEBRU
Given Names:
HIWOT TADESSE
Date of Birth:
1994-05-12
Gender:
Female
Marital Status:
Married
Religion:
Muslim
Job:
House Maid
Education level:
Skills:
ID Number:
AGENCY0001
Mobile Number:
+251911000000
Languages:
Arabic
Number Of Children:
0
E-Mail:
hiwot.example@example.com
Work Experience:
Saudi Arabia - 6 years

Passport Information
Passport Number:
EQ0000001
Passport Issue Place:
Addis Abeba
Passport Issue Date:
2024-02-01
Passport Expiry Date:
2034-01-31

Address
Country:
Ethiopia
City:
Dessie
Address:
Dessie 05

Relative Contact Necessity
Relative name:
MUNIR ALEMU
Relative kinship:
Husband
Relative phone:
0911222333
Relative address:
Kombolcha
`;

test("a copied EPRO page becomes a candidate", () => {
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);

  assert.equal(values.passportNumber, "EQ0000001");
  assert.equal(values.gender, "Female");
  assert.equal(values.maritalStatus, "Married");
  assert.equal(values.occupation, "House Maid");
  assert.equal(values.email, "hiwot.example@example.com");
  assert.equal(values.numberOfChildren, "0");
  assert.equal(values.city, "Dessie");
});

test("given names are the first name and the father's name, the surname is the grandfather's", () => {
  // Ethiopian names are given, father, grandfather, and a passport prints the first two together.
  // The insurance form asks for exactly those three under exactly those headings, so this split
  // is the whole of the transform between the two sites.
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.firstName, "HIWOT");
  assert.equal(values.middleName, "TADESSE");
  assert.equal(values.lastName, "GEBRU");
});

test("the relative's address does not become the candidate's", () => {
  // "Address" matches inside "Relative address". Left unresolved, the kin's town is filed as
  // where the candidate lives — wrong in a way no one would catch by reading the result.
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.address, "Dessie 05");
  assert.equal(values.relativeCity, "Kombolcha");
  assert.equal(values.relativeName, "MUNIR ALEMU");
  assert.equal(values.relativePhone, "0911222333");
});

test("dates arrive as the record holds them", () => {
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.dateOfBirth, "1994-05-12");
  assert.equal(values.passportIssueDate, "2024-02-01");
  assert.equal(values.passportExpiryDate, "2034-01-31");
});

test("a years-abroad summary becomes a country and a count", () => {
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.worksIn, "Saudi Arabia");
  assert.equal(values.experienceAbroadYears, "6");
});

test("an empty label takes nothing from the label after it", () => {
  // Education level is printed with no value. The danger is not the blank, it is that the blank
  // swallows "Skills:" and everything to the next label it recognises.
  const { values, missing } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.qualification, undefined);
  assert.ok(missing.some((m) => m.label === "Education level"));
  assert.equal(values.phoneNumber, "+251911000000");
});

test("a label with no field behind it is reported, not dropped", () => {
  // How an unmapped label makes itself known. The agency's own ID looks like a labour ID and is
  // deliberately not treated as one, so it has to surface somewhere a person will see it.
  const { unrecognised } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  const labels = unrecognised.map((u) => u.label);
  assert.ok(labels.includes("ID Number"));
  assert.equal(unrecognised.find((u) => u.label === "ID Number")?.value, "AGENCY0001");
});

test("a similarly named label is not the same label", () => {
  const { values } = readFromText(EPRO_PAGE, EPRO_CANDIDATE);
  assert.equal(values.phoneNumber, "+251911000000");
  assert.equal(values.numberOfChildren, "0");
});

test("three labels on one line, as the contract block prints them", () => {
  // The employer block is a row, not a column: copying it can put every label and value on a
  // single line, which a line-by-line parse would read as one enormous address.
  const row =
    "LOCATION:  شارع الملك فهد، حي النزهة  VISA NUMBER:  1900000001  MOBILE NUMBER:  966500000000";
  const { values } = readFromText(row, EPRO_SPONSOR);

  assert.equal(values.visaNumber, "1900000001");
  assert.equal(values.sponsorPhone, "966500000000");
  assert.equal(
    values.sponsorAddress,
    "شارع الملك فهد، حي النزهة",
  );
});

test("a web address is not a label", () => {
  const { unrecognised } = readFromText("Source:\nhttps://example.com/page\n", EPRO_CANDIDATE);
  assert.ok(!unrecognised.some((u) => u.label.toLowerCase() === "https"));
});

test("dates that cannot be understood are handed back untouched", () => {
  assert.equal(toIsoDate("1994-05-12"), "1994-05-12");
  assert.equal(toIsoDate("2024/2/1"), "2024-02-01");
  assert.equal(toIsoDate("25/12/2024"), "2024-12-25");
  assert.equal(toIsoDate("12/25/2024"), "2024-12-25");
  assert.equal(toIsoDate("sometime in May"), "sometime in May");
});

test("a phone keeps its digits and loses its decoration", () => {
  assert.equal(tidyPhone("+251 911 000 000"), "+251911000000");
  assert.equal(tidyPhone("(091) 122-2333"), "0911222333");
});

test("a portal that prints the whole name in one box still fills three", () => {
  // SimbaFlow asks for a name in one box; most portals want the parts. Whichever way a page
  // prints it, what lands on the record is the same three fields.
  const page = "Full Name:\nHIWOT TADESSE GEBRU\nPassport Number:\nEQ0000001\n";
  const oneBox: SiteManifest = {
    id: "one-box",
    title: "A portal with one name box",
    hosts: [],
    direction: "read",
    fields: [
      { label: "Full Name", key: "fullName" },
      { label: "Passport Number", key: "passportNumber" },
    ],
  };

  const { values } = readFromText(page, oneBox);
  assert.equal(values.firstName, "HIWOT");
  assert.equal(values.middleName, "TADESSE");
  assert.equal(values.lastName, "GEBRU");
});
