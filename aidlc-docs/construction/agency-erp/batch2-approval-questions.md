# Unit 6 Code Gen — Batch 2 Approval

**Batch 2** (Steps 5–6) complete. API build succeeded (0 errors).

## Delivered

| Step | What |
|------|------|
| 5 | `GetPipelineFunnelQuery` — active workflow stages + Active candidate counts by `CurrentStageId` |
| 6 | `DashboardModule` — `GET /api/dashboard/pipeline-funnel` (`candidate.read`) |

## Response shape

```json
{
  "isSuccess": true,
  "data": {
    "stages": [
      { "stageId": "...", "stageName": "Embassy", "sortOrder": 1, "isFinalStage": false, "count": 12 }
    ],
    "totalCandidates": 40,
    "unassignedCount": 2
  }
}
```

`unassignedCount` = Active candidates with null `CurrentStageId`.

## Question 1
Approve Batch 2 and continue?

A) **Approve** — start Batch 3 (Steps 7–11: frontend admin/partners, harden partners, license UI, overview funnel)

B) **Approve** — pause (manual QA / review first)

C) **Request changes** (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
