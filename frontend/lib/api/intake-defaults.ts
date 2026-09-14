import useSWR from "swr";

export type IntakeDefaults = {
  gender: string;
  occupation: string;
  religion: string;
  nationality: string;
  passportType: string;
  maritalStatus: string;
  countryOfTravel: string;
  contractPeriod: string;
};

const fetcher = async (url: string) => {
  const res = await fetch(url);
  if (!res.ok) throw new Error("Could not load the intake defaults");
  return (await res.json()).data as IntakeDefaults;
};

/**
 * What a blank candidate form starts with. Loaded for new records only — an edit must never have
 * a saved answer replaced by an agency default.
 */
export function useIntakeDefaults(enabled = true) {
  const { data, error, mutate } = useSWR(
    enabled ? "/api/proxy/settings/intake-defaults" : null,
    fetcher,
    { revalidateOnFocus: false },
  );
  return { defaults: data, error, mutate };
}

export async function saveIntakeDefaults(body: IntakeDefaults): Promise<void> {
  const res = await fetch("/api/proxy/settings/intake-defaults", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const j = await res.json().catch(() => ({}));
    throw new Error(j?.error || "Could not save the intake defaults");
  }
}
