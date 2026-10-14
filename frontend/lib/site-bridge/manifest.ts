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
  title: "EPRO — contract parties",
  hosts: [],
  direction: "read",
  fields: [
    { label: "Location", key: "sponsorAddress" },
    { label: "Visa Number", key: "visaNumber" },
    { label: "Mobile Number", key: "sponsorPhone", kind: "phone" },
  ],
  note: "The employer's name sits above the labels rather than beside one, so it is picked separately.",
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
    { label: "Visa Type", key: "visaType", kind: "select", fixed: "Work" },
    { label: "Current Nationality", key: "nationality", kind: "select", fixed: "Ethiopia" },
    { label: "Nearest Saudi Mission", kind: "select", fixed: "Addis Ababa" },
    { label: "Number of Entries", kind: "select", fixed: "Single" },
    { label: "Visa Validity", kind: "select", fixed: "90 Days" },
    { label: "Duration Of Stay", kind: "select", fixed: "90" },
    { label: "Occupation", key: "occupation", kind: "select" },

    // ── Personal information ──
    // The Arabic name is four boxes and we hold none of it. Transliterating an Ethiopian name
    // into Arabic is not a thing to do automatically on a visa application — a wrong spelling
    // here is a name that does not match the passport at the border.
    { label: "Arabic First Name" },
    { label: "Arabic Father Name" },
    { label: "Arabic Grand Name" },
    { label: "Arabic Family Name" },
    { label: "First Name", key: "firstName" },
    { label: "Second/Father Name", key: "middleName" },
    { label: "Other/G.Father Name", key: "lastName" },
    // The form wants four names and an Ethiopian one has three. Confirmed with the agency: the
    // family name is the grandfather's, so it repeats the box above rather than being left to a
    // person to work out on every application.
    { label: "Last/Family Name", key: "lastName" },
    { label: "Passport Number", key: "passportNumber" },
    { label: "Passport Type", key: "passportType", kind: "select", fixed: "Normal" },
    { label: "Issuing Country", key: "country", kind: "select", fixed: "Ethiopia" },
    { label: "Date of Issue", key: "passportIssueDate", kind: "date" },
    { label: "Date of Expiry", key: "passportExpiryDate", kind: "date" },
    { label: "Place of Birth", key: "placeOfBirth" },
    { label: "Date of Birth", key: "dateOfBirth", kind: "date" },
    { label: "ID Number", key: "nationalId" },
    { label: "RELIGION", key: "religion", kind: "select" },
    { label: "Marital Status", key: "maritalStatus", kind: "select" },
    { label: "Gender", key: "gender", kind: "select" },
    { label: "Qualification", key: "qualification" },
    { label: "Source of Degree", fixed: "none" },
    // The house the candidate is going to, which is the sponsor's.
    { label: "KSA Home Address", key: "sponsorAddress" },
    // The agency's address, not the candidate's — confirmed with the agency, and the box has a
    // tooltip saying so. Filling it from the candidate would put a housemaid's gmail address on
    // a consular application as the office contact.
    { label: "Email Address", key: "agencyEmail" },

    // ── Sponsor information ──
    { label: "Sponsor Name", key: "sponsorName" },
    { label: "Sponsor ID Number", key: "sponsorIdNumber" },
    { label: "Sponsor Address", key: "sponsorAddress" },
    { label: "Phone Number", key: "sponsorPhone", kind: "phone" },
    { label: "Transport Mode", kind: "select", fixed: "Air" },
    { label: "Purpose", fixed: "Work" },
  ],
  note:
    "DocumentNumber, Classify Occupations, Port of Entry, Expected Entry Date To KSA and Flight " +
    "Number are left to the person: the first two were unclear on the form and the last three " +
    "belong to a booking that may not exist yet.",
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
