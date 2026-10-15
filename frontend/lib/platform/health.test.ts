import { test } from "node:test";
import assert from "node:assert/strict";
import { ago, attentionReasons } from "@/lib/platform/health";

const now = new Date("2026-10-15T12:00:00Z");

test("a healthy agency needs nothing", () => {
  assert.deepEqual(
    attentionReasons({ status: 0, activeUsers: 3, maxUsers: 10, lastSignInAt: "2026-10-14T09:00:00Z" }, now),
    [],
  );
});

test("an idle agency, one at its limit and open help requests are flagged", () => {
  assert.deepEqual(
    attentionReasons(
      { status: 0, activeUsers: 5, maxUsers: 5, lastSignInAt: "2026-09-01T09:00:00Z", openSupportTickets: 2 },
      now,
    ),
    ["2 open help requests", "No sign-in for 44 days", "At its user limit"],
  );
});

test("an agency nobody has used is noticed", () => {
  assert.ok(attentionReasons({ status: 0, lastSignInAt: null }, now).includes("Nobody has signed in yet"));
});

test("suspended agencies are judged elsewhere", () => {
  assert.deepEqual(attentionReasons({ status: 1, lastSignInAt: null }, now), []);
});

test("a licence running out is flagged", () => {
  assert.ok(
    attentionReasons({ status: 0, lastSignInAt: "2026-10-15T09:00:00Z", licenseExpiresAt: "2026-10-25" }, now)
      .includes("Licence expires in 10 days"),
  );
});

test("ago reads naturally", () => {
  assert.equal(ago(null, now), "never");
  assert.equal(ago("2026-10-15T08:00:00Z", now), "today");
  assert.equal(ago("2026-10-12T08:00:00Z", now), "3 days ago");
});
