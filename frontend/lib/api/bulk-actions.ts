/** Who a bulk action was applied to, and who it was refused for and why. */
export type BulkActionResult = {
  succeeded: string[];
  failed: { candidateId: string; error: string }[];
};

/**
 * Sends one board action for many candidates.
 *
 * The API runs each candidate through the row's own command, so a refusal comes back per candidate
 * in `failed` rather than as an error for the batch. An error is thrown only when nothing was
 * attempted — no permission, nothing selected, or the request itself failed.
 */
export async function postBulkAction(url: string, body: Record<string, unknown>): Promise<BulkActionResult> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const json = await res.json().catch(() => null);
  if (!res.ok || !json?.isSuccess) throw new Error(json?.error || "The action could not be done");
  return {
    succeeded: json.data?.succeeded ?? [],
    failed: json.data?.failed ?? [],
  };
}

const notesOrNull = (notes?: string) => (notes?.trim() ? notes.trim() : null);

export const embassyBulkApi = {
  bookMedical: (candidateIds: string[], notes?: string) =>
    postBulkAction("/api/proxy/embassy/bulk/medical/book", { candidateIds, notes: notesOrNull(notes) }),
  unbookMedical: (candidateIds: string[], notes?: string) =>
    postBulkAction("/api/proxy/embassy/bulk/medical/unbook", { candidateIds, notes: notesOrNull(notes) }),
  recordMedicalResult: (candidateIds: string[], result: string, notes?: string) =>
    postBulkAction("/api/proxy/embassy/bulk/medical/result", { candidateIds, result, notes: notesOrNull(notes) }),
  bookTasheer: (candidateIds: string[], appointmentDate: string, notes?: string) =>
    postBulkAction("/api/proxy/embassy/bulk/tasheer/book", { candidateIds, appointmentDate, notes: notesOrNull(notes) }),
  recordTasheerResult: (candidateIds: string[], result: string, notes?: string) =>
    postBulkAction("/api/proxy/embassy/bulk/tasheer/result", { candidateIds, result, notes: notesOrNull(notes) }),
};

export const lmisBulkApi = {
  markInsurancePaid: (candidateIds: string[], notes?: string) =>
    postBulkAction("/api/proxy/lmis/bulk/insurance/paid", { candidateIds, notes: notesOrNull(notes) }),
  markInsuranceUnpaid: (candidateIds: string[], notes?: string) =>
    postBulkAction("/api/proxy/lmis/bulk/insurance/unpaid", { candidateIds, notes: notesOrNull(notes) }),
};
