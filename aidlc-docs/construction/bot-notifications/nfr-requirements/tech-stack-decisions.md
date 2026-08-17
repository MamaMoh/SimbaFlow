# Tech Stack Decisions — Unit 7: Bot & Notifications

## Confirmed stack (inherited)

| Component | Technology | Unit 7 notes |
|-----------|------------|--------------|
| API | Carter + MediatR where useful | `BotModule` for config/link; handlers for push |
| Realtime | Existing SignalR hub | Mount frontend only |
| Notifier | `ICandidateNotifier` | Swap NoOp → Telegram |
| Identity | ApplicationUser | `TelegramChatId`, language field |
| Frontend | Next.js + SWR + sonner | `/admin/bot`, `/settings/bot` |
| PBT | FsCheck | TEST-70–78 |

## Unit 7–specific decisions

### Decision 1: Phased Telegram-first
- **Choice**: Telegram + SignalR mount + lightweight push; defer WhatsApp & rule CRUD
- **Rationale**: FD plan Q1/Q2=A
- **Trade-off**: US-9.02 / full US-9.05–9.06 later

### Decision 2: Long-polling host
- **Choice**: `BackgroundService` + `getUpdates` (no public webhook in v1)
- **Rationale**: FD Q3=A; fits self-hosted Docker without ingress URL
- **Trade-off**: Single active poller assumed; multi-replica needs leader election later

### Decision 3: Token via environment only
- **Choice**: `Telegram:BotToken` from appsettings/env; UI masked status + Test
- **Rationale**: FD approval Q2=A
- **Trade-off**: Ops rotates token via redeploy/secret store; no in-app rotation

### Decision 4: Telegram.Bot library
- **Choice**: Prefer official `Telegram.Bot` NuGet over raw HttpClient
- **Rationale**: Less boilerplate for sendDocument/getUpdates; well maintained
- **Trade-off**: Extra dependency; pin version in csproj

### Decision 5: Lightweight notification map
- **Choice**: Code templates for stage-change + departure notify; `NotificationDelivery` log
- **Rationale**: FD Q4=A
- **Trade-off**: No admin rule editor until later unit/batch

### Decision 6: Core commands only
- **Choice**: `/link`, `/status`, `/lang`, `/cv`; reject write commands politely
- **Rationale**: FD Q5=A
- **Trade-off**: Field medical/arrival still web-only

### Decision 7: SignalR mount
- **Choice**: Wire provider + listener in authenticated layout
- **Rationale**: FD Q6=A; closes US-11.03 gap
- **Trade-off**: More websocket load; keep existing event shapes
