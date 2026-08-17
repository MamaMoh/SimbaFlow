# Unit 3 Code Gen — Batch 3 Approval

**Batch 2** approved (continue). **Batch 3** (Steps 10–12) complete. Frontend typecheck OK.

## Delivered

### API clients
- `frontend/lib/api/embassy.ts` — board hooks + medical/tasheer/visa intents + SignalR revalidate
- `frontend/lib/api/lmis.ts` — board hook + insurance/milestone + next-milestone helper

### Shared UI
- `StatusUpdateSheet` + `TrackChip`
- `EmbassyRowActions` (embassy + case-executive variants)
- `LmisRowActions` (insurance, milestone, document upload, transitions)

### Pages
| Route | Permission |
|-------|------------|
| `/workflow/embassy` | `embassy.read` |
| `/workflow/case-executive` | `embassy.case_view` |
| `/workflow/lmis` | `lmis.read` |

Nav: **Case Executive** added between Embassy and LMIS.

## Question 1
Approve Batch 3 and continue?

A) **Approve** — start Batch 4 (Steps 13–15: tests + code summary)

B) **Approve** — pause (manual UI QA first)

C) **Request changes** (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
