/**
 * Whether a candidate may have a board action done to them, and if not, why not.
 *
 * One place for the rules, read by both the row's ⋯ menu and the board's Actions menu, so ticking
 * twenty candidates can never offer something the row would not. Each rule answers null when the
 * action is allowed, or the reason in words a desk officer would use: "medical is already Fit".
 *
 * They mirror the server, which applies the same rules again when the action is sent — see the
 * Embassy and LMIS command handlers. The row menu used to offer Book medical on an Expired medical
 * and not on an Unfit one, the other way round from the server, so the one it offered failed and
 * the one it needed was missing.
 */

/** A status as the board shows it; a candidate with none recorded yet reads as "". */
type Status = string | null | undefined;

const norm = (value: Status) => (value ?? "").trim();
const is = (value: Status, ...options: string[]) =>
  options.some((o) => o.toLowerCase() === norm(value).toLowerCase());

// ──── LMIS: insurance ────

export function markInsurancePaidBlock(insurance: Status): string | null {
  if (is(insurance, "", "Insurance Unpaid")) return null;
  return `insurance is already ${norm(insurance)}`;
}

export function markInsuranceUnpaidBlock(insurance: Status): string | null {
  if (is(insurance, "")) return "no insurance is recorded yet";
  if (is(insurance, "Insurance Unpaid")) return "insurance is already unpaid";
  return null;
}

// ──── Embassy: medical ────

/** Pending or not started, or Unfit and being rebooked. */
export function bookMedicalBlock(medical: Status): string | null {
  if (is(medical, "", "Pending", "Unfit")) return null;
  return `medical is already ${norm(medical)}`;
}

export function unbookMedicalBlock(medical: Status): string | null {
  if (is(medical, "Booked")) return null;
  return is(medical, "", "Pending") ? "medical is not booked" : `medical is ${norm(medical)}, not booked`;
}

export function recordMedicalResultBlock(medical: Status): string | null {
  if (is(medical, "Booked")) return null;
  return is(medical, "", "Pending") ? "medical is not booked yet" : `medical is already ${norm(medical)}`;
}

// ──── Embassy: Tasheer ────

/** Pending or not started, or Expired and being rebooked. */
export function bookTasheerBlock(tasheer: Status): string | null {
  if (is(tasheer, "", "Pending", "Expired")) return null;
  return `Tasheer is already ${norm(tasheer)}`;
}

export function recordTasheerResultBlock(tasheer: Status): string | null {
  if (is(tasheer, "Booked")) return null;
  return is(tasheer, "", "Pending") ? "Tasheer is not booked yet" : `Tasheer is already ${norm(tasheer)}`;
}
