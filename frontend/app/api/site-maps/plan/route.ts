import { NextRequest, NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";
import { getCachedServerSession } from "@/lib/api/session-cache";
import { manifestById, type FieldKey } from "@/lib/site-bridge/manifest";
import { joinName } from "@/lib/site-bridge/names";
import { fillPlan, type CandidateValues } from "@/lib/site-bridge/write";

/**
 * One candidate, ready to be typed into somebody else's form.
 *
 * The plan is built here rather than in the extension, and that is the whole point of the
 * arrangement: the extension receives a list of labels and the text to put beside them, and
 * needs to understand none of it. Which box, which spelling, which date format, what to leave
 * alone — all of it is decided on this side, where it can be changed by a deploy and is covered
 * by tests that run on every build.
 *
 * What the extension does with this is mechanical, and deliberately so. An extension that knew
 * what a sponsor was would need to be re-published every time we learnt something new about
 * one.
 */

/** A date column may arrive as a full timestamp; the forms want the day. */
const day = (value: unknown): string =>
  typeof value === "string" && /^\d{4}-\d{2}-\d{2}/.test(value) ? value.slice(0, 10) : "";

/**
 * Gender crosses the wire as the enum it is stored as, and every form downstream wants a word.
 * Done here so neither the map nor the extension has to know that 1 means female.
 */
const genderWord = (value: unknown): string => {
  if (value === 1 || value === "1") return "Female";
  if (value === 0 || value === "0") return "Male";
  return typeof value === "string" ? value : "";
};

const text = (value: unknown): string =>
  value === null || value === undefined ? "" : String(value);

/**
 * The record as the maps name things.
 *
 * Mostly a straight copy, because the field keys were chosen to match what the candidate form
 * already calls these — the three exceptions are the ones above plus nationality, which falls
 * back to the country that issued the passport.
 */
function toValues(c: Record<string, unknown>): CandidateValues {
  const direct: FieldKey[] = [
    "firstName",
    "middleName",
    "lastName",
    "localFullName",
    "maritalStatus",
    "religion",
    "numberOfChildren",
    "occupation",
    "qualification",
    "otherLanguages",
    "passportNumber",
    "labourId",
    "nationalId",
    "passportType",
    "passportPlaceOfIssue",
    "placeOfBirth",
    "phoneNumber",
    "email",
    "address",
    "city",
    "country",
    "region",
    "subcity",
    "woreda",
    "countryOfTravel",
    "visaNumber",
    "visaType",
    "eNumber",
    "sponsorName",
    "sponsorArabicName",
    "sponsorIdNumber",
    "sponsorPhone",
    "sponsorAddress",
    "agentName",
    "worksIn",
  ];

  const values: CandidateValues = {};
  for (const key of direct) values[key] = text(c[key]);

  values.gender = genderWord(c.gender);
  values.dateOfBirth = day(c.dateOfBirth);
  values.passportIssueDate = day(c.passportIssueDate);
  values.passportExpiryDate = day(c.passportExpiryDate);
  values.nationality = text(c.nationality) || text(c.country);

  // Assembled, not stored: SimbaFlow holds the parts, and a form with a single name box wants
  // them back together. Built from the same rule that divides them, so the two cannot disagree.
  values.fullName = joinName({
    firstName: text(c.firstName),
    middleName: text(c.middleName),
    lastName: text(c.lastName),
  });

  return values;
}

export async function GET(request: NextRequest) {
  const session = await getCachedServerSession();
  if (!session?.user?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const site = request.nextUrl.searchParams.get("site") ?? "";
  const candidateId = request.nextUrl.searchParams.get("candidateId") ?? "";

  const manifest = manifestById(site);
  if (!manifest || manifest.direction !== "write") {
    return NextResponse.json({ error: "No such form" }, { status: 404 });
  }
  if (!/^[0-9a-fA-F-]{36}$/.test(candidateId)) {
    return NextResponse.json({ error: "No such candidate" }, { status: 400 });
  }

  try {
    // The agency's own contact block comes along for the ride: the visa application asks for an
    // email and means the office's. Fetched beside the candidate rather than after it, because
    // one of the two failing should not cost the round trip for the other.
    const [candidateBody, agencyBody, answersBody] = await Promise.all([
      apiClient.get(`/candidates/${candidateId}`) as Promise<Record<string, unknown>>,
      (apiClient.get(`/settings/agency-contact`) as Promise<Record<string, unknown>>).catch(
        () => null,
      ),
      // Both of these are settings rather than facts about the candidate, and an agency that has
      // set neither still gets a correct form — so neither is allowed to fail the whole plan.
      (apiClient.get(`/settings/portal-answers`) as Promise<Record<string, unknown>>).catch(
        () => null,
      ),
    ]);

    const body = candidateBody;
    const candidate = (body?.data ?? body) as Record<string, unknown>;
    if (!candidate?.id) {
      return NextResponse.json({ error: "No such candidate" }, { status: 404 });
    }

    const agency = ((agencyBody?.data ?? agencyBody) ?? {}) as Record<string, unknown>;
    const values = toValues(candidate);
    values.agencyEmail = text(agency.contactEmail ?? agency.ContactEmail);

    const rows = ((answersBody?.data ?? answersBody ?? []) as unknown[]).filter(
      (r): r is Record<string, unknown> => typeof r === "object" && r !== null,
    );
    const answers: Record<string, string> = {};
    for (const row of rows) {
      if (text(row.siteId ?? row.SiteId) !== manifest.id) continue;
      answers[text(row.label ?? row.Label)] = text(row.value ?? row.Value);
    }

    const plan = fillPlan(values, manifest, answers);

    return NextResponse.json({
      data: {
        site: { id: manifest.id, title: manifest.title, note: manifest.note ?? null },
        candidate: {
          id: candidate.id,
          name: [candidate.firstName, candidate.middleName, candidate.lastName]
            .filter(Boolean)
            .join(" "),
          passportNumber: text(candidate.passportNumber),
        },
        plan,
      },
    });
  } catch (e: unknown) {
    const err = e as { message?: string; status?: number };
    return NextResponse.json(
      { error: err?.message || "Could not build the form" },
      { status: err?.status || 500 },
    );
  }
}
