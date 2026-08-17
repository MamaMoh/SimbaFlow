# Functional Design Plan — Unit 7: Bot & Notifications

## Unit Context
- **Unit**: Bot & Notifications (Unit 7)
- **Stories**: US-9.01–US-9.09 (Bot), US-11.03 (SignalR real-time)
- **Dependencies**: Unit 1 (SignalR hub), Unit 2 (candidate status), Units 3–4 (domain events), Unit 4 (`ICandidateNotifier` NoOp seam)
- **Brownfield seams**: SignalR hub/broadcaster, `NoOpCandidateNotifier`, user `TelegramChatId`/`WhatsAppNumber`, permissions `bot.*` / `notification.*`, docker-compose env placeholders

## Already in codebase (audit 2026-07-29)

| Area | Status |
|------|--------|
| `SimbaFlowHub` + `SignalRBroadcaster` | Exists; candidate stage/status handlers broadcast |
| Frontend `signalr-provider` + `notification-listener` | Exist but **not mounted** in layout |
| `ICandidateNotifier` → `NoOpCandidateNotifier` | Travel "Mark Notified" calls NoOp |
| User chat ID fields + bot permissions/roles | Seeded |
| Telegram/WhatsApp clients, webhooks, polling | **Not built** |
| NotificationRule / Template / Delivery entities | **Not built** |
| Bot admin / notification config UI | **Not built** |
| `appsettings` Bot sections | Missing (only compose/.env placeholders) |

## Plan

- [x] Step 1: Confirm Unit 7 delivery scope — DONE (Q1=A phased)
- [x] Step 2: Channel priority — DONE (Q2=A Telegram first)
- [x] Step 3: Bot host model — DONE (Q3=A long-polling)
- [x] Step 4: Notification rules depth — DONE (Q4=A lightweight v1)
- [x] Step 5: SignalR wiring — DONE (Q6=A mount + expand)
- [x] Step 5b: Bot commands — DONE (Q5=A core set)
- [x] Step 6: Generate FD artifacts + approval questions — DONE

**Plan answers**: Q1–Q6 all A (user: `a,a,a,a,a`; Q6 treated as A recommended)

**Artifacts**: `construction/bot-notifications/functional-design/`  
**Approval**: `construction/bot-notifications/functional-design-approval-questions.md`
