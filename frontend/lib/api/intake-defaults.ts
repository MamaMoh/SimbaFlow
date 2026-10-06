import useSWR, { type KeyedMutator } from "swr";

export type AgencySkill = {
  id: string;
  name: string;
  builtInKey?: string | null;
  isBuiltIn: boolean;
  isDefaultSelected: boolean;
  sortOrder: number;
};

export type IntakeDefaults = {
  gender: string;
  occupation: string;
  religion: string;
  nationality: string;
  passportType: string;
  maritalStatus: string;
  countryOfTravel: string;
  contractPeriod: string;
  cookingLevel: string;
  /** Which CV layout this agency prints. */
  cvTemplate: string;
  /** The layouts to choose from, described for the person choosing. */
  cvTemplates?: { value: string; name: string; description: string }[];
  skills: AgencySkill[];
};

const KEY = "/api/proxy/settings/intake-defaults";

const GUID =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function isPersistedSkillId(id: string | undefined | null): boolean {
  return !!id && GUID.test(id);
}

function readString(source: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === "string") return value;
    if (typeof value === "number") return String(value);
  }
  return "";
}

function readBool(source: Record<string, unknown>, ...keys: string[]): boolean {
  for (const key of keys) {
    if (source[key] === true) return true;
  }
  return false;
}

function parseSkills(data: Record<string, unknown>): AgencySkill[] {
  const raw = data.skills ?? data.Skills;
  if (!Array.isArray(raw)) return [];
  const result: AgencySkill[] = [];
  raw.forEach((item, index) => {
    if (!item || typeof item !== "object") return;
    const row = item as Record<string, unknown>;
    const name = readString(row, "name", "Name").trim();
    if (!name) return;
    const sort =
      typeof row.sortOrder === "number"
        ? row.sortOrder
        : typeof row.SortOrder === "number"
          ? row.SortOrder
          : index;
    result.push({
      id: readString(row, "id", "Id") || `missing-${index}`,
      name,
      builtInKey: readString(row, "builtInKey", "BuiltInKey") || null,
      isBuiltIn: readBool(row, "isBuiltIn", "IsBuiltIn"),
      isDefaultSelected: readBool(row, "isDefaultSelected", "IsDefaultSelected"),
      sortOrder: sort,
    });
  });
  return result;
}

/**
 * Whether a stored default is actually set. Empty string is the "ask each time" answer;
 * any other string — including Male, which is stored as `"0"` — is a real default.
 */
export function hasStoredDefault(value: string | undefined | null): boolean {
  return value != null && value !== "";
}

/**
 * Value to give a Radix Select: the stored answer, or `noneToken` when genuinely unset.
 */
export function intakeSelectValue(
  stored: string | undefined | null,
  noneToken: string
): string {
  return hasStoredDefault(stored) ? (stored as string) : noneToken;
}

/**
 * Shown in the settings card. Radix SelectValue with no children waits on an ItemText portal
 * from a closed dropdown, which is how a GET that clearly had values still painted as empty.
 */
export function intakeSelectLabel(
  stored: string | undefined | null,
  options: readonly { value: string; label: string }[],
  unsetLabel: string
): string {
  if (!hasStoredDefault(stored)) return unsetLabel;
  const value = stored as string;
  return options.find((o) => o.value === value)?.label ?? value;
}

/**
 * The settings page and the new-candidate form both read this payload. The API has used both
 * camelCase and PascalCase on the same object, so every field is resolved either way rather than
 * silently becoming "" — which the settings UI then draws as "No default".
 */
export function parseIntakeDefaults(payload: unknown): IntakeDefaults | undefined {
  if (!payload || typeof payload !== "object") return undefined;
  const root = payload as Record<string, unknown>;
  const data = (root.data ?? root.Data ?? payload) as Record<string, unknown>;
  if (!data || typeof data !== "object") return undefined;
  return {
    gender: readString(data, "gender", "Gender"),
    occupation: readString(data, "occupation", "Occupation"),
    religion: readString(data, "religion", "Religion"),
    nationality: readString(data, "nationality", "Nationality"),
    passportType: readString(data, "passportType", "PassportType"),
    maritalStatus: readString(data, "maritalStatus", "MaritalStatus"),
    countryOfTravel: readString(data, "countryOfTravel", "CountryOfTravel"),
    contractPeriod: readString(data, "contractPeriod", "ContractPeriod"),
    cookingLevel: readString(data, "cookingLevel", "CookingLevel"),
    cvTemplate: readString(data, "cvTemplate", "CvTemplate") || "layout3",
    cvTemplates: Array.isArray(data.cvTemplates)
      ? (data.cvTemplates as IntakeDefaults["cvTemplates"])
      : Array.isArray(data.CvTemplates)
        ? (data.CvTemplates as IntakeDefaults["cvTemplates"])
        : undefined,
    skills: parseSkills(data),
  };
}

const fetcher = async (url: string) => {
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error("Could not load the intake defaults");
  return parseIntakeDefaults(await res.json());
};

/**
 * What a blank candidate form starts with. Loaded for new records only — an edit must never have
 * a saved answer replaced by an agency default.
 */
export function useIntakeDefaults(enabled = true): {
  defaults: IntakeDefaults | undefined;
  error: Error | undefined;
  mutate: KeyedMutator<IntakeDefaults | undefined>;
} {
  const { data, error, mutate } = useSWR(enabled ? KEY : null, fetcher, {
    revalidateOnFocus: false,
  });
  return { defaults: data, error, mutate };
}

export async function saveIntakeDefaults(body: IntakeDefaults): Promise<IntakeDefaults> {
  const res = await fetch(KEY, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      gender: body.gender,
      occupation: body.occupation,
      religion: body.religion,
      nationality: body.nationality,
      passportType: body.passportType,
      maritalStatus: body.maritalStatus,
      countryOfTravel: body.countryOfTravel,
      contractPeriod: body.contractPeriod,
      cookingLevel: body.cookingLevel,
      cvTemplate: body.cvTemplate,
      skills: (body.skills ?? []).map((skill, index) => ({
        id: isPersistedSkillId(skill.id) ? skill.id : undefined,
        name: skill.name,
        isDefaultSelected: skill.isDefaultSelected,
        sortOrder: index,
      })),
    }),
    cache: "no-store",
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json?.error || "Could not save the intake defaults");
  }
  return parseIntakeDefaults(json) ?? body;
}
