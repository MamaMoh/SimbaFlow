import { test } from "node:test";
import assert from "node:assert/strict";
import { auditTrailUrl } from "@/lib/api/audit";

test("only the filters that are set go into the query", () => {
  assert.equal(auditTrailUrl({ kind: "changes", page: 1 }), "/api/proxy/audit?kind=changes&page=1");
  assert.equal(
    auditTrailUrl({ kind: "views", page: 2, from: "2026-10-01", to: "2026-10-15", userId: "u1" }),
    "/api/proxy/audit?kind=views&page=2&from=2026-10-01&to=2026-10-15&userId=u1",
  );
});

test("failed-only applies to changes; a view never fails", () => {
  assert.match(auditTrailUrl({ kind: "changes", page: 1, failedOnly: true }), /failedOnly=true/);
  assert.doesNotMatch(auditTrailUrl({ kind: "views", page: 1, failedOnly: true }), /failedOnly/);
});
