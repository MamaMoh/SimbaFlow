import useSWR, { type KeyedMutator } from "swr";

export type IntakeDefaults = {
  gender: string;
  occupation: string;
  religion: string;
  nationality: string;
  passportType: string;
  maritalStatus: string;
  countryOfTravel: string;
  contractPeriod: string;
  /** Which CV layout this agency prints. */
  cvTemplate: string;
  /** The layouts to choose from, described for the person choosing. */
  cvTemplates?: { value: string; name: string; description: string }[];
};

const KEY = "/api/proxy/settings/intake-defaults";

function readString(source: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === "string") return value;
    if (typeof value === "number") return String(value);
  }
  return "";
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
    cvTemplate: readString(data, "cvTemplate", "CvTemplate") || "layout3",
    cvTemplates: Array.isArray(data.cvTemplates)
      ? (data.cvTemplates as IntakeDefaults["cvTemplates"])
      : Array.isArray(data.CvTemplates)
        ? (data.CvTemplates as IntakeDefaults["cvTemplates"])
        : undefined,
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
      cvTemplate: body.cvTemplate,
    }),
    cache: "no-store",
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json?.error || "Could not save the intake defaults");
  }
  return parseIntakeDefaults(json) ?? body;
}
