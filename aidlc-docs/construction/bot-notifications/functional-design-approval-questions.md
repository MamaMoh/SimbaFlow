# Unit 7 Functional Design — Approval

**Unit**: Bot & Notifications  
**Stories**: US-9.01–US-9.09 (phased), US-11.03  
**Plan answers**: Q1–Q6 all **A** (user `a,a,a,a,a` + Q6 recommended A)

**Artifacts**:
- `construction/bot-notifications/functional-design/domain-entities.md`
- `construction/bot-notifications/functional-design/business-logic-model.md`
- `construction/bot-notifications/functional-design/business-rules.md`
- `construction/bot-notifications/functional-design/frontend-components.md`

## Design summary

- **Telegram** long-polling hosted service; WhatsApp deferred (stub UI)
- **Commands**: `/register`/`/link`, `/status`, `/lang`, `/cv` + stage-change push
- **Defer**: `/medical`, `/arrived`, full NotificationRule engine
- Replace **NoOp** `ICandidateNotifier` with Telegram sender for departure notify
- **Mount** SignalR provider/listener in layout
- Token via **env/appsettings** (masked status in admin UI)

## Question 1
Approve Unit 7 Functional Design?

A) **Approve** — proceed to Unit 7 NFR Requirements

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Telegram bot token handling for v1:

A) **Env / appsettings only** (recommended) — admin UI shows configured/missing + test; no token paste in browser

B) **Admin UI can set/rotate token** (encrypted at rest in DB)

C) **Both** — env default; UI override for SuperAdmin

D) Other (please describe after [Answer]: tag below)

[Answer]: A
