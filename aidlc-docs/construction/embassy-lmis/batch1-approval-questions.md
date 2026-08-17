# Unit 3 Code Gen — Batch 1 Approval

**Batch 1** (Steps 1–5) complete. Build succeeded; **42/42** tests passed.

## Delivered

| Step | What |
|------|------|
| 1 | `Candidate.StageEnteredAt` + index; set on register + transition |
| 2 | Engine: status metadata, `UpdateStatusChainAsync`, mirror cleanup on `RemoveFromSource`, pending-event sequence safety |
| 3 | Case Executive stage + visa Ready/Submitted mirror in seeder; `WorkflowDefinitionUpgrader` (Program + provision) |
| 4 | Permissions: `embassy.case_view`, `embassy.case_submit`, `embassy.visa_outcome`, `lmis.document` + role maps |
| 5 | Migration `20260721133610_AddCandidateStageEnteredAt` |

## Question 1
Approve Batch 1 and continue?

A) **Approve** — start Batch 2 (Steps 6–9: EmbassyModule + LmisModule APIs)

B) **Approve** — pause (manual QA / review first)

C) **Request changes** (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
