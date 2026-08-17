# Unit 7 NFR Requirements — Approval

**Unit**: Bot & Notifications  
**Artifacts**:
- `construction/bot-notifications/nfr-requirements/nfr-requirements.md`
- `construction/bot-notifications/nfr-requirements/tech-stack-decisions.md`

## Highlights

- `/status` &lt;3s; `/cv` &lt;10s; stage push best-effort &lt;30s
- Token from env only; never in API responses
- Workflow / Mark Notified must not fail when Telegram is down
- PBT TEST-70–78 (tenant scope, link codes, no write commands, no token leak)
- Prefer `Telegram.Bot` + long-polling `BackgroundService`; mount SignalR in layout

## Question 1
Approve Unit 7 NFR Requirements?

A) **Approve** — proceed to Unit 7 NFR Design

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
