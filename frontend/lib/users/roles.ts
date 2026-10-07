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

/** Runs the platform and has no agency of their own. */
export const PLATFORM_ROLES = ["PlatformAdmin"] as const;

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

export function isPlatformRole(role: string | undefined | null): boolean {
  return !!role && (PLATFORM_ROLES as readonly string[]).includes(role);
}
