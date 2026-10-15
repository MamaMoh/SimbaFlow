import assert from "node:assert/strict";
import { test } from "node:test";
import {
  CLIPBOARD_LIFETIME_MS,
  CLIPBOARD_PREFIX,
  decodePayload,
  encodePayload,
  toPayload,
  type PlanResponse,
} from "./clipboard";

/** Invented, as every fixture here is. */
const RESPONSE: PlanResponse = {
  site: { id: "insurance-member", title: "Insurance — add insured" },
  candidate: { id: "c0ffee00-0000-4000-8000-000000000001", name: "Hiwot Tadesse Gebru", passportNumber: "EQ0000001" },
  plan: {
    steps: [
      { key: "firstName", label: "First Name", kind: "text", value: "Hiwot", display: "Hiwot", alternatives: [], keepTheirs: false },
      { key: "gender", label: "Gender", kind: "select", value: "Female", display: "Female", alternatives: ["F"], keepTheirs: false },
    ],
    missing: [{ key: "email", label: "Email" }],
    repeatButton: "Add Member",
  },
};

test("a payload is one line the extension can recognise by its first characters", () => {
  const text = encodePayload(toPayload(RESPONSE, 0));
  assert.ok(text.startsWith(CLIPBOARD_PREFIX));
  assert.equal(text.includes("\n"), false);
});

test("it survives the round trip whole", () => {
  const payload = toPayload(RESPONSE, 1_000);
  assert.deepEqual(decodePayload(encodePayload(payload)), payload);
});

test("names that are not ASCII survive it too", () => {
  // btoa alone throws on these, and an Amharic or Arabic name is ordinary here, not an edge.
  const payload = toPayload(
    { ...RESPONSE, candidate: { ...RESPONSE.candidate, name: "ሕይወት ታደሰ · محمد" } },
    0,
  );
  assert.equal(decodePayload(encodePayload(payload))?.candidate.name, "ሕይወት ታደሰ · محمد");
});

test("the form's own labels travel with it, so the wrong page can be told apart", () => {
  // Both the boxes being filled and the ones being left: every label is evidence of the form.
  assert.deepEqual(toPayload(RESPONSE, 0).recognise, ["First Name", "Gender", "Email"]);
});

test("a copy goes stale", () => {
  assert.equal(toPayload(RESPONSE, 5_000).expiresAt, 5_000 + CLIPBOARD_LIFETIME_MS);
});

test("ordinary text is not a payload", () => {
  assert.equal(decodePayload("Hiwot Tadesse"), null);
  assert.equal(decodePayload(`${CLIPBOARD_PREFIX}not base64 at all!`), null);
});

/**
 * The extension decodes this with its own code, which cannot import this file. So the format is
 * pinned here as literal bytes: if the encoding changes, this fails, and the failure is the
 * reminder that extension/paste.js has to change with it.
 */
test("the wire format is what extension/paste.js expects", () => {
  const text = encodePayload({
    site: { id: "s", title: "t" },
    candidate: { id: "i", name: "é", passportNumber: "" },
    plan: { steps: [], missing: [] },
    recognise: [],
    expiresAt: 1,
  });
  assert.equal(
    text,
    "simbaflow-fill:1:eyJzaXRlIjp7ImlkIjoicyIsInRpdGxlIjoidCJ9LCJjYW5kaWRhdGUiOnsiaWQiOiJpIiwibmFtZSI6IsOpIiwicGFzc3BvcnROdW1iZXIiOiIifSwicGxhbiI6eyJzdGVwcyI6W10sIm1pc3NpbmciOltdfSwicmVjb2duaXNlIjpbXSwiZXhwaXJlc0F0IjoxfQ==",
  );
});
