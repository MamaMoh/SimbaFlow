/**
 * Every permission that opens one of the boards My work has a tab for, the same list the API
 * accepts. Holding any one of them is enough to have a tab, so it is also what puts My work in
 * the sidebar: an embassy officer without candidate.read still has work waiting.
 */
export const MY_WORK_PERMISSIONS = [
  "candidate.update",
  "workflow.view",
  "embassy.read",
  "embassy.case_view",
  "lmis.read",
  "travel.read",
  "arrival.read",
  "arrival.exception",
  "commission.read",
  "system.admin",
];
