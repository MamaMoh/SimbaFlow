# Unit 7 — Functional Design Plan Approval

**Unit**: Bot & Notifications  
**Plan**: `construction/plans/bot-notifications-functional-design-plan.md`  
**Stories**: US-9.01–US-9.09, US-11.03

## Brownfield note

SignalR hub/broadcaster and a NoOp `ICandidateNotifier` already exist. User Telegram/WhatsApp fields and `bot.*` / `notification.*` permissions are seeded. Telegram/WhatsApp clients, rule engine, delivery ledger, and admin UI are **greenfield**.

---

## Question 1 — Unit 7 delivery scope
Unit of Work lists Telegram + WhatsApp + rules engine + admin UI + SignalR event wiring. How much in this construction unit?

A) **Phased — Telegram + notifier + SignalR first** (recommended): Telegram connect/register/status/CV/lang + replace NoOp notifier for departure notify + mount SignalR in layout; defer WhatsApp + full rule-engine admin to Batch 2 or Unit 8 follow-up

B) **Full Unit 7 as scoped** — Telegram + WhatsApp + rules engine + delivery monitoring + all bot commands in one unit

C) **SignalR + in-app only** — skip external bots this unit; wire real-time toasts and mark-notified via email/stub only

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Messaging channels
A) **Telegram first** (recommended) — WhatsApp Business API stub/config UI only until Telegram is solid

B) **Telegram + WhatsApp together** — both send/receive paths in v1

C) **WhatsApp first** — Telegram deferred

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Telegram host model
A) **Long-polling `BackgroundService`** (recommended for self-hosted Docker) — no public webhook URL required in v1

B) **Webhook endpoint** — `POST /api/bot/telegram/webhook` + public HTTPS required

C) **Both** — polling for dev; webhook for production via config switch

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4 — Notification rules engine
A) **Lightweight v1** (recommended) — hard-coded event→channel map for stage transitions + departure notify; admin UI for bot token + enable/disable channels; full rule CRUD later

B) **Full rule engine** — NotificationRule/Template/Delivery CRUD + role targeting in this unit

C) **No rules UI** — code-only event handlers; config via appsettings only

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5 — Bot commands in v1
Stories list `/status`, `/medical`, `/arrived`, `/cv`, `/lang`, `/register`.

A) **Core set** (recommended): `/register`, `/status`, `/lang`, `/cv` + push on stage change; defer `/medical` and `/arrived` write actions to Batch 2

B) **All commands** including medical update and arrival confirm via bot

C) **Lookup only** — `/register`, `/status`, `/lang` (no CV, no write actions)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6 — SignalR (US-11.03)
A) **Mount + expand** (recommended) — wire `SignalRProvider`/`NotificationListener` into layout; keep existing candidate broadcasts; add notification toast channel for bot-linked events

B) **Mount only** — enable existing toasts; no new event types

C) **Defer SignalR UI wiring** — backend broadcasts stay; frontend mount in later batch

D) Other (please describe after [Answer]: tag below)

[Answer]: A
