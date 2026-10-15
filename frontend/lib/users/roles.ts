/**
 * The roles a platform administrator may hand out, split by who the person works for.
 *
 * Written once because it was written twice: the create form offered PlatformAdmin to a platform
 * user, the edit form offered only the agency roles, so a PlatformAdmin opened for editing showed
 * an empty Role box — their role was set, it simply was not among the options, and a Select with
 * no matching item renders its placeholder.
 *
 * SuperAdmin is in neither list. It bypasses permission checks rather than holding permissions, so
 * it is granted by the bootstrap seeder and not from a form.
 */

/**
 * Runs the platform and has no agency of their own. PlatformSupport is the support desk: it sees
 * and helps every agency but cannot change one, enter one, or touch billing.
 */
export const PLATFORM_ROLES = ["PlatformAdmin", "PlatformSupport"] as const;

/** Works inside one agency. */
export const AGENCY_ROLES = [
  "AgencyOwner",
  "OfficeManager",
  "EmbassyOfficer",
  "CaseExecutive",
  "FinanceOfficer",
  "FieldAgent",
  "DataEntryClerk",
  "Auditor",
  "NotificationManager",
] as const;

/**
 * The agency's owner. Given by the platform team when the agency is provisioned, and moved only by
 * them: an owner who could make a second owner could be removed by that second owner.
 */
export const OWNER_ROLE = "AgencyOwner";

/** The agency roles this person may hand out: all of them for the platform team, else all but owner. */
export function grantableAgencyRoles(isPlatformTeam: boolean): readonly string[] {
  return isPlatformTeam ? AGENCY_ROLES : AGENCY_ROLES.filter((r) => r !== OWNER_ROLE);
}

export function isPlatformRole(role: string | undefined | null): boolean {
  return !!role && (PLATFORM_ROLES as readonly string[]).includes(role);
}
