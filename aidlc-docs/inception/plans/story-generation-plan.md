# Story Generation Plan

## Methodology

Stories will be organized using a **Domain-Based + Persona-Based hybrid** approach:
- Primary organization by business domain (matching the 10 feature modules)
- Each story explicitly identifies which persona(s) are involved
- Stories follow the "As a [persona], I want [action], so that [benefit]" format
- Acceptance criteria use Given/When/Then (Gherkin) format
- Stories comply with INVEST criteria (Independent, Negotiable, Valuable, Estimable, Small, Testable)

## Execution Plan

- [x] Step 1: Generate personas.md — Define all 11 user personas with characteristics, goals, frustrations
- [x] Step 2: Generate Epic structure — Define epics aligned with feature modules
- [x] Step 3: Generate stories for Candidate Management (FR-01)
- [x] Step 4: Generate stories for Configurable Workflow Engine (FR-02)
- [x] Step 5: Generate stories for Embassy & Visa Processing (FR-03)
- [x] Step 6: Generate stories for LMIS (FR-04)
- [x] Step 7: Generate stories for Travel & Logistics (FR-05)
- [x] Step 8: Generate stories for Arrival & Deployment Tracking (FR-06)
- [x] Step 9: Generate stories for Commission & Finance (FR-07)
- [x] Step 10: Generate stories for Agency ERP (FR-08)
- [x] Step 11: Generate stories for Telegram/WhatsApp Bot (FR-09)
- [x] Step 12: Generate stories for Reporting & Analytics (FR-10)
- [x] Step 13: Generate cross-cutting stories (Auth, Multi-tenancy, Real-time)
- [x] Step 14: Map personas to stories matrix
- [x] Step 15: Final validation — INVEST compliance check

---

## Clarifying Questions

Please answer the following questions to guide story generation.

## Question 1
What story granularity level is appropriate for this project?

A) Coarse — One story per major feature (e.g., "As an Embassy Officer, I want to manage the full embassy process") — ~20-30 total stories

B) Medium — One story per user action within a feature (e.g., "As an Embassy Officer, I want to book a medical appointment for a candidate") — ~60-80 total stories

C) Fine — One story per atomic interaction (e.g., "As an Embassy Officer, I want to select a date for the medical appointment") — ~150+ total stories

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 2
What acceptance criteria format do you prefer?

A) Given/When/Then (Gherkin) — Formal, testable, suitable for BDD automation

B) Bullet-point checklist — Simpler, less formal, easier to read

C) Both — Gherkin for complex logic, bullet points for simple CRUD

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
How should stories handle the configurable nature of the workflow? (Each agency may have different stages)

A) Write stories for the DEFAULT workflow only (8-stage flow from spec) — configuration stories separately

B) Write stories generically (e.g., "transition candidate to next stage") — applicable to any workflow config

C) Write stories for default flow AND separate stories for the configuration/admin experience

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 4
For the Telegram/WhatsApp bot, should bot interactions be:

A) Separate stories per bot command — each bot action is its own story

B) Integrated into feature stories — bot access is an acceptance criterion within each feature story

C) Both — Core bot infrastructure as separate stories, feature-specific bot actions as acceptance criteria

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 5
Should stories include non-functional acceptance criteria (performance, security) or keep those separate?

A) Include NFR criteria inline — each story includes relevant performance/security acceptance criteria

B) Separate NFR stories — dedicated stories for "the system must respond within 500ms" etc.

C) Hybrid — Critical NFRs inline (e.g., auth required), systemic NFRs as separate stories

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 6
What is the primary language for story content?

A) English only

B) Amharic only

C) English with Amharic translations for user-facing text/labels

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---
