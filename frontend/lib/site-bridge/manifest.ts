/**
 * What the other sites call the things we already hold.
 *
 * Four journeys, all of them currently done by eye and by hand:
 *
 *   EPRO  → candidate        a new registration, retyped out of the portal
 *   EPRO  → sponsor details  the employer block off the contract page
 *   candidate → insurance    sixteen boxes, once per insured member
 *   candidate → visa platform
 *
 * The first two read a page, the last two fill one, and it would be natural to write those as
 * two unrelated features. They are not: both are a translation between a field of ours and a
 * label of theirs, pointed in opposite directions. So there is one table per site and it serves
 * whichever direction that site is used in — add a field once and both halves learn it.
 *
 * Nothing in this file touches the DOM or React, on purpose. The browser extension that fills
 * the insurance form needs exactly this knowledge and must not carry its own copy: a second copy
 * is a second thing to update, on twenty laptops, when a portal renames a box. The extension
 * fetches these maps from SimbaFlow at runtime instead, so a portal change is a deploy here
 * rather than a re-install there.
 */

/**
 * Every field these maps can carry, named as the candidate form already names them, so a value
 * read out of a portal can be handed to the registration form without translation.
 *
 * Two of them are not candidate fields at all but shapes a portal presents instead of ours:
 * `givenNames` is "HIWOT TADESSE" on one line, and `workExperienceSummary` is
 * "Saudi Arabia - 6 years". They are expanded on the way in and never stored.
 */
export const FIELD_KEYS = [
  // identity
  "firstName",
  "middleName",
  "lastName",
  "localFullName",
  "gender",
  "dateOfBirth",
  "maritalStatus",
  "religion",
  "numberOfChildren",
  "occupation",
  "qualification",
  "nationality",
  "placeOfBirth",
  "passportType",
  "visaType",
  "otherLanguages",
  // references
  "passportNumber",
  "labourId",
  "nationalId",
  "passportPlaceOfIssue",
  "passportIssueDate",
  "passportExpiryDate",
  // contact
  "phoneNumber",
  "email",
  "address",
  "city",
  "country",
  "region",
  "subcity",
  "woreda",
  // travel
  "countryOfTravel",
  // sponsor and visa
  "visaNumber",
  "eNumber",
  "sponsorName",
  "sponsorArabicName",
  "sponsorIdNumber",
  "sponsorPhone",
  "sponsorAddress",
  "agentName",
  "agencyEmail",
  // relative
  "relativeName",
  "relativeKinship",
  "relativePhone",
  "relativeCity",
  // work history
  "worksIn",
  "experienceAbroadYears",
  // derived for a form that asks for it, never stored
  "title",
  /** The year out of the date of birth, for a form with a year dropdown beside the date box. */
  "birthYear",
  /**
   * The whole name in one box, as SimbaFlow asks for it and as some portals print it.
   *
   * Real in both directions: split into the three parts on the way in, and assembled from them
   * on the way out for a form that wants one box rather than four.
   */
  "fullName",
  // portal shapes, expanded on the way in
  "givenNames",
  "workExperienceSummary",
] as const;

export type FieldKey = (typeof FIELD_KEYS)[number];

/**
 * How a value behaves, which is the only thing the two directions disagree about.
 *
 * A date is the clearest case: the portal shows `1996-01-01`, the insurance form wants
 * `mm/dd/yyyy`, and the record holds ISO. Reading normalises to ISO and writing formats out of
 * it, so neither side has to know about the other.
 *
 * `select` matters more than it looks. You cannot paste into a dropdown — the copy-a-field-at-a-
 * time approach simply cannot fill Region or Country Of Destination, and that is the single
 * reason the extension is worth building rather than a column of copy buttons.
 */
export type ValueKind = "text" | "date" | "phone" | "number" | "select";

/** One label on their page, and what it means here. */
export type ReadField = {
  /** Printed exactly as the portal prints it, minus the colon. Matched case-insensitively. */
  label: string;
  key: FieldKey;
  kind?: ValueKind;
};

/** One box on their form, and what we put in it. */
export type WriteField = {
  label: string;
  /**
   * The box's own `id` or `name` on their form, when we have been shown the page and know it.
   *
   * The printed label is the right thing to match on in general — it is what a person sees, and
   * it survives the markup being rebuilt. But it is a guess about the markup: it assumes the
   * label sits where the walk can find it, and on a form that lays a row out as label, box,
   * label, box, inside wrappers a date picker has built, that assumption fails quietly and puts
   * the expiry date into Place of Birth.
   *
   * Where the form has been inspected, the id is not a guess. The label stays as the fallback,
   * so a form that renames its ids still fills.
   */
  match?: string[];
  /**
   * Absent when the form asks for something we do not hold. The box is then reported as needing
   * a person rather than quietly skipped, which is the difference between "we cannot fill this"
   * and "we forgot about this".
   */
  key?: FieldKey;
  /**
   * The same answer on every application this agency files — the nearest mission, the validity,
   * the transport mode. Not candidate data and not worth storing per candidate.
   *
   * Hard-coded for now and belonging in tenant settings before a second agency uses this form,
   * because the nearest Saudi mission is only Addis Ababa from Addis Ababa.
   */
  fixed?: string;
  kind?: ValueKind;
  /**
   * Not overwritten when the box already has something in it. A form that arrives with a
   * sensible default of its own — today's date, the country — should keep it.
   */
  keepTheirs?: boolean;
};

/**
 * A value printed under a heading rather than beside a label.
 *
 * The contract-parties block on the portal prints "EMPLOYER INFO" and then the employer's name
 * on the next line, with nothing joining the two — the labelled fields start underneath. There
 * is no label to match, so the heading is named instead and the line under it is the value.
 *
 * Deliberately narrow. Only a heading this file names is read this way: a page of bare lines
 * gives nothing to tell a heading from a value, and guessing there would invent fields out of
 * people's names.
 */
export type LeadField = {
  /** The heading, printed as the portal prints it. Matched case-insensitively, on its own line. */
  after: string;
  key: FieldKey;
  kind?: ValueKind;
};

export type SiteManifest = {
  id: string;
  /** Shown to the person choosing what they are pasting or filling. */
  title: string;
  /**
   * Hostnames this map applies to. The extension narrows its permissions to exactly these, so
   * an empty list means it runs nowhere — which is the right default until the real hostname is
   * known. Filling one in here is what switches a site on.
   */
  hosts: string[];
  direction: "read" | "write";
  fields: (ReadField | WriteField)[];
  /** Values that sit under a heading rather than beside a label. Read-direction only. */
  leads?: LeadField[];
  /**
   * For a form that is filled repeatedly before being submitted once — the insurance form adds
   * one insured member at a time. Names the button that banks a row, which is as far as we go:
   * the extension fills and a person clicks.
   */
  repeatButton?: string;
  note?: string;
};

/**
 * The EPRO candidate page.
 *
 * `ID Number` and the `Name` under EPRO Information are deliberately absent. The first is the
 * agency's own reference rather than the government labour ID it resembles, and the second is a
 * label so generic it would capture any stray "Name:" on the page. Both still reach the person
 * reviewing the paste, under "not recognised", which is a better place for a guess than a field.
 */
export const EPRO_CANDIDATE: SiteManifest = {
  id: "epro-candidate",
  title: "EPRO — candidate page",
  hosts: [],
  direction: "read",
  fields: [
    { label: "Surname", key: "lastName" },
    { label: "Given Names", key: "givenNames" },
    { label: "Date of Birth", key: "dateOfBirth", kind: "date" },
    { label: "Gender", key: "gender" },
    { label: "Marital Status", key: "maritalStatus" },
    { label: "Religion", key: "religion" },
    { label: "Job", key: "occupation" },
    { label: "Education level", key: "qualification" },
    { label: "Mobile Number", key: "phoneNumber", kind: "phone" },
    { label: "E-Mail", key: "email" },
    { label: "Languages", key: "otherLanguages" },
    { label: "Number Of Children", key: "numberOfChildren", kind: "number" },
    { label: "Work Experience", key: "workExperienceSummary" },
    { label: "Passport Number", key: "passportNumber" },
    { label: "Passport Issue Place", key: "passportPlaceOfIssue" },
    { label: "Passport Issue Date", key: "passportIssueDate", kind: "date" },
    { label: "Passport Expiry Date", key: "passportExpiryDate", kind: "date" },
    { label: "Country", key: "country" },
    { label: "City", key: "city" },
    { label: "Address", key: "address" },
    { label: "Relative name", key: "relativeName" },
    { label: "Relative kinship", key: "relativeKinship" },
    { label: "Relative phone", key: "relativePhone", kind: "phone" },
    // The portal calls it an address and prints a town. We hold no free-text address for a
    // relative, only the parts of one, and the town is the part this is.
    { label: "Relative address", key: "relativeCity" },
  ],
};

/**
 * The contract parties block — the employer half.
 *
 * The location arrives in Arabic and is kept exactly as it came. It is the address a driver will
 * be given, and a transliteration of it is worth nothing to anybody.
 */
export const EPRO_SPONSOR: SiteManifest = {
  id: "epro-sponsor",
  title: "Musaned — contract parties",
  hosts: [],
  direction: "read",
  fields: [
    { label: "Location", key: "sponsorAddress" },
    { label: "Visa Number", key: "visaNumber" },
    { label: "Mobile Number", key: "sponsorPhone", kind: "phone" },
  ],
  // The employer's name is printed under the heading rather than beside a label, which is why
  // it is read as a lead and not as a field. It is the one thing on this block somebody would
  // otherwise have to retype, and it is the box the enjaze form calls Sponsor Name.
  leads: [{ after: "Employer Info", key: "sponsorName" }],
};

/**
 * The insurance portal's Add Insured form.
 *
 * Sixteen boxes per person and a batch is twenty people. Every one of them is already on the
 * candidate — including Labor ID, which is why that column went onto the boards.
 *
 * Insurance Branch and Number Of Insured are not here: they belong to the batch, not the member,
 * and are set once by the person before any of this starts.
 */
export const INSURANCE_MEMBER: SiteManifest = {
  id: "insurance-member",
  title: "Insurance — add insured",
  hosts: [],
  direction: "write",
  repeatButton: "Add Member",
  fields: [
    { label: "Title", key: "title", kind: "select" },
    { label: "First Name", key: "firstName" },
    { label: "Father Name", key: "middleName" },
    { label: "Grand Father Name", key: "lastName" },
    { label: "Gender", key: "gender", kind: "select" },
    { label: "Date Of Birth", key: "dateOfBirth", kind: "date" },
    { label: "Passport Number", key: "passportNumber" },
    { label: "Labor ID", key: "labourId" },
    { label: "Phone", key: "phoneNumber", kind: "phone" },
    { label: "Email", key: "email" },
    { label: "Woreda", key: "woreda" },
    { label: "Sub city", key: "subcity" },
    { label: "Region", key: "region", kind: "select" },
    { label: "Country", key: "country", keepTheirs: true },
    { label: "Country Of Destination", key: "countryOfTravel", kind: "select" },
  ],
  note: "Effective Date is left as the form sets it — it is the policy's date, not the candidate's.",
};


/**
 * The enjaz visa application.
 *
 * Three sections and about forty boxes, and the shape of it is different from the insurance
 * form in a way the map has to carry: a third of these are not about the candidate at all. The
 * nearest mission, the validity, the duration, the transport mode and the purpose are the same
 * on every application this agency has ever filed. They are `fixed` here rather than typed
 * forty times or stored forty thousand times.
 *
 * What is deliberately absent, and why:
 *
 *  - The two declarations at the foot. "I hereby declare that the information provided is true"
 *    is a person's attestation on a government application, and a program has no business
 *    ticking it. The person reads the filled form and ticks it themselves.
 *  - The previous-nationality and travelled-in-five-years radios, for the same reason in a
 *    smaller way: they are answers about the candidate's history that we do not hold, and a
 *    guessed "No" on a visa application is a false statement.
 *  - The photo. It is an upload with its own rules — white background, uniform — and the file
 *    is already on the candidate; filling it is a separate job from filling text.
 */
export const VISA_APPLICATION: SiteManifest = {
  id: "visa-application",
  title: "Visa platform — application",
  hosts: [],
  direction: "write",
  fields: [
    // ── Visa information ──
    { label: "Visa Type", match: ["VisaKind"], key: "visaType", kind: "select", fixed: "Work" },
    {
      label: "Current Nationality",
      match: ["NATIONALITY"],
      key: "nationality",
      kind: "select",
      fixed: "Ethiopia",
    },
    { label: "Nearest Saudi Mission", match: ["EmbassyCode"], kind: "select", fixed: "Addis Ababa" },
    // These three are filled by the form itself once Visa Type is chosen, and are empty until
    // that round trip comes back. The filler asks again after a moment rather than reporting
    // them as boxes with no matching option, which is what they look like for the first second.
    { label: "Number of Entries", match: ["NUMBER_OF_ENTRIES"], kind: "select", fixed: "Single" },
    { label: "Visa Validity", match: ["Number_Entry_Day"], kind: "select", fixed: "90 Days" },
    { label: "Duration Of Stay", match: ["RESIDENCY_IN_KSA"], kind: "select", fixed: "90" },
    // A text box on this form, not a dropdown — the dropdown beside it is the occupation
    // classification, which is a different question and is left to the person.
    { label: "Occupation", match: ["OccupationName"], key: "occupation" },

    // ── Personal information ──
    // The Arabic name is four boxes and we hold none of it. Transliterating an Ethiopian name
    // into Arabic is not a thing to do automatically on a visa application — a wrong spelling
    // here is a name that does not match the passport at the border.
    { label: "Arabic First Name", match: ["AFIRSTNAME"] },
    { label: "Arabic Father Name", match: ["AFATHER"] },
    { label: "Arabic Grand Name", match: ["AGRAND"] },
    { label: "Arabic Family Name", match: ["AFAMILY"] },
    { label: "First Name", match: ["EFIRSTNAME"], key: "firstName" },
    { label: "Second/Father Name", match: ["EFATHER"], key: "middleName" },
    // Other/G.Father Name — EGRAND — is deliberately absent. The form wants four names and an
    // Ethiopian one has three. Confirmed with the agency: the family name is the grandfather's,
    // so filling this one too would put the same word in two boxes of a visa application.
    { label: "Last/Family Name", match: ["EFAMILY"], key: "lastName" },
    { label: "Passport Number", match: ["PASSPORTnumber"], key: "passportNumber" },
    {
      label: "Passport Type",
      match: ["PASSPORType"],
      key: "passportType",
      kind: "select",
      fixed: "Normal",
    },
    // Matched by id and not by label alone: a second box on this form carries the same printed
    // label inside the tourism-visa section, which is hidden and has no options in it at all.
    {
      label: "Issuing Country",
      match: ["PASSPORT_ISSUE_PLACE"],
      key: "country",
      kind: "select",
      fixed: "Ethiopia",
    },
    { label: "Date of Issue", match: ["PASSPORT_ISSUE_DATE"], key: "passportIssueDate", kind: "date" },
    // Their own spelling of the id, lower-case final letter and all. Copied exactly on purpose.
    {
      label: "Date of Expiry",
      match: ["PASSPORT_EXPIRY_DATe"],
      key: "passportExpiryDate",
      kind: "date",
    },
    { label: "Place of Birth", match: ["BIRTH_PLACE"], key: "placeOfBirth" },
    { label: "Year of Birth", match: ["BirthYearDDL"], key: "birthYear", kind: "select" },
    { label: "Date of Birth", match: ["BIRTH_DATE"], key: "dateOfBirth", kind: "date" },
    { label: "ID Number", match: ["PersonId"], key: "nationalId" },
    { label: "Religion", match: ["RELIGION"], key: "religion", kind: "select" },
    { label: "Marital Status", match: ["SOCIAL_STATUS"], key: "maritalStatus", kind: "select" },
    { label: "Gender", match: ["Sex"], key: "gender", kind: "select" },
    { label: "Qualification", match: ["DEGREE"], key: "qualification" },
    { label: "Source of Degree", match: ["DEGREE_SOURCE"], fixed: "none" },
    // The house the candidate is going to, which is the sponsor's.
    { label: "KSA Home Address", match: ["ADDRESS_HOME"], key: "sponsorAddress" },
    // The agency's address, not the candidate's — confirmed with the agency, and the box has a
    // tooltip saying so. Filling it from the candidate would put a housemaid's gmail address on
    // a consular application as the office contact.
    { label: "Email Address", match: ["Personal_Email"], key: "agencyEmail" },

    // ── Sponsor information ──
    { label: "Sponsor Name", match: ["SPONSER_NAME"], key: "sponsorName" },
    { label: "Sponsor ID Number", match: ["SPONSER_NUMBER"], key: "sponsorIdNumber" },
    { label: "Sponsor Address", match: ["SPONSER_ADDRESS"], key: "sponsorAddress" },
    { label: "Phone Number", match: ["SPONSER_PHONE"], key: "sponsorPhone", kind: "phone" },
    { label: "Transport Mode", match: ["COMING_THROUGH"], kind: "select", fixed: "By Air" },
    { label: "Purpose", match: ["porpose"], fixed: "Work" },
  ],
  note:
    "Classify Occupations, Port of Entry, Expected Entry Date To KSA and Flight Number are left " +
    "to the person: the first belongs to a list this does not map, and the rest belong to a " +
    "booking that may not exist yet. The three date boxes are pickers that cannot be typed in, " +
    "so what is written into them is reported for checking rather than counted as done.",
};

export const SITE_MANIFESTS: SiteManifest[] = [
  EPRO_CANDIDATE,
  EPRO_SPONSOR,
  INSURANCE_MEMBER,
  VISA_APPLICATION,
];

export function manifestById(id: string): SiteManifest | undefined {
  return SITE_MANIFESTS.find((m) => m.id === id);
}

/** Every map that applies to a page, which is how the extension decides whether to offer itself. */
export function manifestsForHost(host: string): SiteManifest[] {
  const clean = host.toLowerCase().replace(/^www\./, "");
  return SITE_MANIFESTS.filter((m) =>
    m.hosts.some((h) => clean === h || clean.endsWith(`.${h}`)),
  );
}

/**
 * The boxes on a form that this agency answers the same way every time.
 *
 * What the settings screen offers to edit, and the only part of a map a person ever sees. The
 * `fixed` value in the map is the starting point rather than the law: an agency filing through a
 * different mission, or on a different validity, changes it here and every application after
 * that follows.
 */
export function standingAnswers(manifest: SiteManifest): WriteField[] {
  if (manifest.direction !== "write") return [];
  return (manifest.fields as WriteField[]).filter((f) => f.fixed !== undefined);
}

/** Every form that has any, for the settings screen. */
export function formsWithStandingAnswers(): { site: SiteManifest; fields: WriteField[] }[] {
  return SITE_MANIFESTS.map((site) => ({ site, fields: standingAnswers(site) })).filter(
    (entry) => entry.fields.length > 0,
  );
}
