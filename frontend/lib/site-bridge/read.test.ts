import assert from "node:assert/strict";
import { test } from "node:test";
import { EPRO_CANDIDATE, EPRO_SPONSOR, SITE_MANIFESTS, type SiteManifest } from "./manifest";
import { bestRead, readFromText, tidyPhone, toDestinationPhone, toIsoDate } from "./read";

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

/**
 * The same page as it actually arrives on the clipboard.
 *
 * The portal renders each label in its own cell with the value in the cell beneath, so copying
 * the page gives label line, value line, and no colon anywhere. Around it come the maintenance
 * banner, the navigation and the section headings, none of which are fields.
 *
 * Invented throughout, as above.
 */
const EPRO_PAGE_WITHOUT_COLONS = `
نعتذر عن تعطل الخدمة مؤقتا
EN
Dashboard
Organizations
HR Pool
Partnership Contracts
Employment Requests

Preview
View Candidate
Personal Information
Surname
GEBRU
Given Names
HIWOT TADESSE
Gender
Female
Date of Birth
1994-05-12
Marital Status
Married
Religion
Muslim
Job
House Maid
Education level
Preparatory School
Skills
Baby Sitting & Cleaning
ID Number
AGENCY0001
Mobile Number
+251911000000
Languages
Arabic
Number Of Children
0
E-Mail
hiwot.example@example.com
Work Experience
Saudi Arabia - 6 years

Passport Information
Passport Number
EQ0000001
Passport Issue Place
Addis Abeba
Passport Issue Date
2024-02-01
Passport Expiry Date
2034-01-31

Address
Country
Ethiopia
City
Dessie

Relative Information
Relative name
MUNIR ALEMU
Relative kinship
Husband
Relative phone
0911222333
Relative address
Kombolcha
`;

test("a portal that prints no colons is still a page we can read", () => {
  // The candidate page puts the label in one cell and the value in the cell under it. Copied,
  // that is two plain lines with nothing between them, and a parse that insists on a colon reads
  // the whole page as not a page at all — which is what the desk saw: a paste that recognised
  // nothing and a Fill button that stayed grey.
  const { values, found } = readFromText(EPRO_PAGE_WITHOUT_COLONS, EPRO_CANDIDATE);

  assert.ok(found.length >= 15, `recognised only ${found.length} fields`);
  assert.equal(values.firstName, "HIWOT");
  assert.equal(values.middleName, "TADESSE");
  assert.equal(values.lastName, "GEBRU");
  assert.equal(values.dateOfBirth, "1994-05-12");
  assert.equal(values.passportNumber, "EQ0000001");
  assert.equal(values.passportIssueDate, "2024-02-01");
  assert.equal(values.passportExpiryDate, "2034-01-31");
  assert.equal(values.passportPlaceOfIssue, "Addis Abeba");
  assert.equal(values.phoneNumber, "+251911000000");
  assert.equal(values.email, "hiwot.example@example.com");
  assert.equal(values.qualification, "Preparatory School");
  assert.equal(values.relativeName, "MUNIR ALEMU");
  assert.equal(values.relativeCity, "Kombolcha");
});

test("a section heading that shares a field's name takes no value", () => {
  // "Address" heads the section and has nothing under it but the next label. Read as a field it
  // would file the candidate's address as the word "Country", which is the kind of wrong that
  // survives review because it looks like something.
  const { values } = readFromText(EPRO_PAGE_WITHOUT_COLONS, EPRO_CANDIDATE);
  assert.equal(values.address, undefined);
  assert.equal(values.country, "Ethiopia");
  assert.equal(values.city, "Dessie");
});

test("the colonless page is recognised as the candidate page and not the contract block", () => {
  const best = bestRead(EPRO_PAGE_WITHOUT_COLONS, [EPRO_CANDIDATE, EPRO_SPONSOR]);
  assert.equal(best?.manifest.id, EPRO_CANDIDATE.id);
});

test("a bare word in prose is not a label", () => {
  // Only a label standing alone on its own line counts. A page that mentions the word in a
  // sentence has not printed a field.
  const { found } = readFromText(
    "The gender of the applicant is recorded elsewhere.\nSee the Job description attached.\n",
    EPRO_CANDIDATE,
  );
  assert.equal(found.length, 0);
});

test("the employer's name is read from under its heading", () => {
  // The contract block as the portal prints it: a heading, the employer's name on the next
  // line with no label joining them, and the labelled fields underneath. Every value below is
  // invented.
  const block = [
    "Contract parties",
    "EMPLOYER INFO",
    "SAMPLE EMPLOYER NAME",
    "LOCATION:",
    "شارع الملك فهد، حي النزهة",
    "VISA NUMBER:",
    "1900000001",
    "MOBILE NUMBER:",
    "966500000000",
  ].join("\n");

  const { values } = readFromText(block, EPRO_SPONSOR);

  assert.equal(values.sponsorName, "SAMPLE EMPLOYER NAME");
  assert.equal(values.visaNumber, "1900000001");
  assert.equal(values.sponsorPhone, "966500000000");
  assert.equal(values.sponsorAddress, "شارع الملك فهد، حي النزهة");
});

test("a heading with a label straight under it has no name of its own", () => {
  // The portal prints the block without the employer named. Reading "Location" as somebody's
  // name would put a word into a visa application that nobody typed.
  const block = "EMPLOYER INFO\nLOCATION:\nRiyadh\n";
  const { values } = readFromText(block, EPRO_SPONSOR);

  assert.equal(values.sponsorName, undefined);
  assert.equal(values.sponsorAddress, "Riyadh");
});

test("the contract block beats the candidate page on a paste of the contract block", () => {
  // Mobile Number is a label on both maps. Whichever recognises more of the page wins, and
  // the employer's name is part of that count.
  const block = [
    "EMPLOYER INFO",
    "SAMPLE EMPLOYER NAME",
    "LOCATION:",
    "Riyadh",
    "VISA NUMBER:",
    "1900000001",
    "MOBILE NUMBER:",
    "966500000000",
  ].join("\n");

  const best = bestRead(block, [EPRO_CANDIDATE, EPRO_SPONSOR]);
  assert.equal(best?.manifest.id, "epro-sponsor");
  assert.equal(best?.result.values.sponsorName, "SAMPLE EMPLOYER NAME");
});

test("a sponsor number printed the way Saudi Arabia writes it becomes one a box can dial", () => {
  // The trunk zero is not part of the number; a box already on +966 took it for one.
  assert.equal(toDestinationPhone("0500000000"), "+966500000000");
  assert.equal(toDestinationPhone("05 00 00 00 00"), "+966500000000");
});

test("a sponsor number that already names its country is left alone", () => {
  assert.equal(toDestinationPhone("+966500000000"), "+966500000000");
  assert.equal(toDestinationPhone("966500000000"), "+966500000000");
});

test("a sponsor number with neither a trunk zero nor a country code gets the code", () => {
  assert.equal(toDestinationPhone("500000000"), "+966500000000");
});

test("nothing pasted stays nothing", () => {
  assert.equal(toDestinationPhone(""), "");
  assert.equal(toDestinationPhone("   "), "");
});

test("a contract block copied out of a table deals its values across the row", () => {
  // The page prints Location, Visa Number and Mobile Number side by side. Copied, the labels
  // arrive together and the values after them.
  const copied = [
    "Contract parties",
    "EMPLOYER INFO",
    "SAMPLE TEST EMPLOYER",
    "LOCATION:",
    "VISA NUMBER:",
    "MOBILE NUMBER:",
    "Sample Street, Test District 10000",
    "1900000001",
    "966500000000",
  ].join("\n");

  const read = bestRead(copied, SITE_MANIFESTS);
  assert.ok(read);
  assert.equal(read.result.values.sponsorAddress, "Sample Street, Test District 10000");
  assert.equal(read.result.values.visaNumber, "1900000001");
  assert.equal(read.result.values.sponsorPhone, "966500000000");
  assert.equal(read.result.values.sponsorName, "SAMPLE TEST EMPLOYER");
});

test("the same block printed down the page still reads the same", () => {
  const copied = [
    "EMPLOYER INFO",
    "SAMPLE TEST EMPLOYER",
    "LOCATION:",
    "Sample Street, Test District 10000",
    "VISA NUMBER:",
    "1900000001",
    "MOBILE NUMBER:",
    "966500000000",
  ].join("\n");

  const read = bestRead(copied, SITE_MANIFESTS);
  assert.ok(read);
  assert.equal(read.result.values.sponsorAddress, "Sample Street, Test District 10000");
  assert.equal(read.result.values.visaNumber, "1900000001");
  assert.equal(read.result.values.sponsorPhone, "966500000000");
});

test("an address that landed in the mobile box is not read as a number", () => {
  // Without the guard this arrives as a phone number of the digits in a street address.
  const copied = ["MOBILE NUMBER:", "Sample Street, Test District"].join("\n");
  const read = readFromText(copied, EPRO_SPONSOR);
  assert.equal(read.values.sponsorPhone, undefined);
});
