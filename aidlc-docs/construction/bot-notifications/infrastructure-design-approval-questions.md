# Unit 7 Infrastructure Design — Approval

**Unit**: Bot & Notifications  
**Artifact**: `construction/bot-notifications/infrastructure-design/infrastructure-design.md`

## Highlights

- Same Compose stack; **no new containers**
- Platform tables: `BotRegistrationChallenges`, `NotificationDeliveries`
- Reuse `TelegramChatId` + `PreferredLanguage` on users
- `Telegram.Bot` + long-poll hosted service; `PollingEnabled` for multi-replica
- Replace NoOp notifier; hook stage-change push
- `BotModule` + `/admin/bot` + `/settings/bot` + SignalR layout mount
- Reuse existing `bot.*` / `notification.*` permissions

## Question 1
Approve Unit 7 Infrastructure Design?

A) **Approve** — proceed to Unit 7 Code Generation plan

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
