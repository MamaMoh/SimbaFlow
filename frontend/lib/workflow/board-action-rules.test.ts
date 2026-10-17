import { strict as assert } from "node:assert";
import test from "node:test";
import {
  bookMedicalBlock,
  bookTasheerBlock,
  markInsurancePaidBlock,
  markInsuranceUnpaidBlock,
  recordMedicalResultBlock,
  recordTasheerResultBlock,
  unbookMedicalBlock,
} from "./board-action-rules";

test("insurance can be marked paid while it is unpaid or not recorded", () => {
  assert.equal(markInsurancePaidBlock(""), null);
  assert.equal(markInsurancePaidBlock(undefined), null);
  assert.equal(markInsurancePaidBlock("Insurance Unpaid"), null);
  assert.equal(markInsurancePaidBlock("Insurance Paid"), "insurance is already Insurance Paid");
  assert.equal(markInsurancePaidBlock("Available"), "insurance is already Available");
});

test("insurance can be marked unpaid only once something was recorded", () => {
  assert.equal(markInsuranceUnpaidBlock("Insurance Paid"), null);
  assert.equal(markInsuranceUnpaidBlock(""), "no insurance is recorded yet");
  assert.equal(markInsuranceUnpaidBlock("Insurance Unpaid"), "insurance is already unpaid");
});

test("a medical is booked from pending or unfit — what the server accepts", () => {
  assert.equal(bookMedicalBlock(""), null);
  assert.equal(bookMedicalBlock("Pending"), null);
  // Rebooking after an unfit result is how the desk tries again.
  assert.equal(bookMedicalBlock("Unfit"), null);
  assert.equal(bookMedicalBlock("Booked"), "medical is already Booked");
  assert.equal(bookMedicalBlock("Fit"), "medical is already Fit");
  // The server refuses this one, so offering it only produced an error.
  assert.notEqual(bookMedicalBlock("Expired"), null);
});

test("only a booked medical can be unbooked or given a result", () => {
  assert.equal(unbookMedicalBlock("Booked"), null);
  assert.equal(recordMedicalResultBlock("Booked"), null);
  assert.equal(unbookMedicalBlock("Pending"), "medical is not booked");
  assert.equal(recordMedicalResultBlock(""), "medical is not booked yet");
  assert.equal(recordMedicalResultBlock("Fit"), "medical is already Fit");
});

test("Tasheer is booked from pending or expired, and given a result once booked", () => {
  assert.equal(bookTasheerBlock(""), null);
  assert.equal(bookTasheerBlock("Expired"), null);
  assert.equal(bookTasheerBlock("Booked"), "Tasheer is already Booked");
  assert.equal(recordTasheerResultBlock("Booked"), null);
  assert.equal(recordTasheerResultBlock("Pending"), "Tasheer is not booked yet");
  assert.equal(recordTasheerResultBlock("Book Done"), "Tasheer is already Book Done");
});

test("the rules read a status whatever its case or spacing", () => {
  assert.equal(bookMedicalBlock(" pending "), null);
  assert.equal(recordTasheerResultBlock("BOOKED"), null);
});
