# User Stories

## Epic 1: Candidate Management (FR-01)

### US-1.01: Register New Candidate
**As a** Data Entry Clerk (Tigist),
**I want to** register a new candidate with their biographical and identity data,
**So that** the candidate enters the system pipeline and can be tracked through all stages.

**Acceptance Criteria:**
```gherkin
Given I am logged in as a Data Entry Clerk
And I navigate to "New Candidate" form
When I fill in required fields (first name, last name, passport number, nationality, date of birth, phone number)
And I fill in optional fields (labour ID, country of travel, office name, contract date)
And I click "Register"
Then the candidate record is created with status "Registered"
And the candidate appears in the New Contract View
And a unique candidate ID is generated
And a status history entry is logged with my user ID and timestamp
And the response returns within 500ms
And the action is audit-logged
```

### US-1.02: Upload Candidate Documents
**As a** Data Entry Clerk (Tigist),
**I want to** upload documents (passport scan, photo, contract) for a candidate,
**So that** all required documents are stored and accessible for processing.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile
When I click "Upload Document"
And I select a file (PDF, JPG, PNG up to 10MB)
And I choose a document type (Passport, Photo, Contract, Other)
Then the file is saved to the server file system
And a reference record is created in the database with file path, type, upload date
And the document appears in the candidate's document list
And a thumbnail preview is generated for images
And the upload action is audit-logged
```

### US-1.03: Search Candidates
**As a** Data Entry Clerk (Tigist),
**I want to** search for existing candidates before creating a new record,
**So that** I avoid duplicate registrations.

**Acceptance Criteria:**
```gherkin
Given I am on the candidate search page
When I enter a search term (name, passport number, or labour ID)
Then matching candidates are displayed within 500ms
And results show: name, passport number, current stage, office
And I can click a result to view the full profile
And search is case-insensitive and supports partial matching
```

### US-1.04: Filter Candidates by Criteria
**As an** Office Manager (Hana),
**I want to** filter the candidate list by stage, status, country, and office,
**So that** I can focus on specific subsets of candidates.

**Acceptance Criteria:**
```gherkin
Given I am on the candidates list page
When I apply filters (stage, status, country of travel, office name, date range)
Then the list updates in real-time showing only matching candidates
And filter selections are preserved during the session
And I can clear all filters with one action
And results include a count of matching candidates
```

### US-1.05: View Candidate Status Timeline
**As an** Office Manager (Hana),
**I want to** view the complete status history of a candidate,
**So that** I can track their progression and identify delays.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile
When I navigate to the "History" tab
Then I see a chronological timeline of all status changes
And each entry shows: previous status, new status, timestamp, user who made the change, notes
And the timeline is ordered newest-first by default
And I can filter by date range
```

### US-1.06: Generate Candidate CV
**As a** Data Entry Clerk (Tigist),
**I want to** auto-generate a CV from a candidate's profile data,
**So that** I can provide standardized CVs to partner agencies without manual formatting.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile with complete biographical data
When I click "Generate CV"
Then a PDF CV is generated using a standard template
And the CV includes: name, photo, nationality, DOB, skills, work history, languages
And the CV is stored in the candidate's document list
And I can download or preview the generated CV
```

### US-1.07: Edit Candidate Information
**As a** Data Entry Clerk (Tigist),
**I want to** update a candidate's information after initial registration,
**So that** I can correct errors or add information received later.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile
And I have edit permission for this candidate
When I click "Edit" and modify any field
And I click "Save"
Then the candidate record is updated
And the previous values are preserved in the audit trail
And a notification is sent to relevant stakeholders if critical fields changed (passport number, country)
And optimistic concurrency prevents overwriting concurrent edits
```

### US-1.08: Soft Delete Candidate
**As an** Office Manager (Hana),
**I want to** remove a candidate from active views without permanently deleting their data,
**So that** invalid or duplicate records don't clutter the pipeline while preserving audit history.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile
And I have delete permission
When I click "Archive Candidate" and confirm
Then the candidate is marked as deleted (soft delete)
And the candidate no longer appears in active views
And the candidate record remains in the database for audit purposes
And the action is logged with reason
```

### US-1.09: View Candidate Documents
**As an** Embassy Officer (Dawit),
**I want to** view all uploaded documents for a candidate,
**So that** I can verify identity and supporting documents during processing.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's profile
When I navigate to the "Documents" tab
Then I see all uploaded documents grouped by type
And I can preview images and PDFs inline
And I can download individual documents
And document metadata shows: type, upload date, uploaded by
And access to documents is read-audit-logged
```

### US-1.10: Assign Labour ID
**As a** Data Entry Clerk (Tigist),
**I want to** assign or update a candidate's Labour ID,
**So that** the candidate is linked to cross-border regulatory channels.

**Acceptance Criteria:**
```gherkin
Given I am editing a candidate's profile
When I enter a Labour ID in the designated field
Then the Labour ID is validated for format correctness
And the Labour ID is saved to the candidate record
And if the Labour ID already exists for another candidate, a warning is shown
And the change is audit-logged
```

---

## Epic 2: Configurable Workflow Engine (FR-02)

### US-2.01: View Workflow Configuration
**As an** Agency Owner (Amir),
**I want to** view my agency's current workflow configuration,
**So that** I understand the stages and transitions my candidates follow.

**Acceptance Criteria:**
```gherkin
Given I am logged in as Agency Owner
When I navigate to "Workflow Configuration"
Then I see a visual representation of all stages in order
And each stage shows its name, allowed statuses, and outbound transitions
And I can see which action buttons are configured for each stage
And the display reflects my agency's specific configuration
```

### US-2.02: Add Workflow Stage
**As an** Agency Owner (Amir),
**I want to** add a new stage to my agency's workflow,
**So that** I can customize the pipeline to match my specific business process.

**Acceptance Criteria:**
```gherkin
Given I am on the workflow configuration page
When I click "Add Stage"
And I enter: stage name, position (order), description
And I define the allowed statuses for this stage
And I click "Save"
Then the new stage is added to my agency's workflow
And existing candidates are not affected (they remain in their current stage)
And the workflow visualization updates to show the new stage
And the change is audit-logged
```

### US-2.03: Define Transition Rules
**As an** Agency Owner (Amir),
**I want to** define the conditions that must be met before a candidate can transition to the next stage,
**So that** the system enforces my business rules automatically.

**Acceptance Criteria:**
```gherkin
Given I am editing a workflow stage
When I click "Add Transition Rule"
And I specify: source stage, target stage, required field conditions (e.g., medical_status = "Fit")
And I specify which roles can execute this transition
And I click "Save"
Then the transition rule is stored in the database
And the action button for this transition only appears when all conditions are met
And only users with the specified roles see the action button
And the rule is validated for circular dependency (no infinite loops)
```

### US-2.04: Configure Dynamic Action Buttons
**As an** Agency Owner (Amir),
**I want to** configure which action buttons appear based on field values,
**So that** my staff sees only the actions available for a candidate's current state.

**Acceptance Criteria:**
```gherkin
Given I am defining a transition rule
When I configure visibility conditions (field name, operator, value)
And I set the button label (e.g., "To Embassy", "To LMIS")
And I optionally set required fields that must be filled before the action can execute
Then the button configuration is saved
And the API returns available actions for each candidate record
And the frontend renders buttons based on server-provided available actions
And the frontend also evaluates conditions optimistically for faster UI
And the server re-validates all conditions on action execution
```

### US-2.05: Configure Stage Statuses
**As an** Agency Owner (Amir),
**I want to** define custom status values for each workflow stage,
**So that** my staff can track detailed progress within each stage.

**Acceptance Criteria:**
```gherkin
Given I am editing a workflow stage
When I click "Manage Statuses"
And I add, edit, or reorder status values
And I mark which statuses are "terminal" (stage-complete triggers)
Then the statuses are saved to the database
And staff see these status options when updating candidates in this stage
And reporting uses these custom statuses for filtering
```

### US-2.06: Configure Parallel Tracks
**As an** Agency Owner (Amir),
**I want to** configure a stage with multiple parallel tracks (e.g., Medical + Tasheer),
**So that** candidates can be processed on both tracks simultaneously.

**Acceptance Criteria:**
```gherkin
Given I am editing a workflow stage (e.g., Embassy)
When I enable "Parallel Tracks" for this stage
And I define track names (e.g., "Medical", "Tasheer")
And I define completion statuses for each track
And I define the combined condition for stage advancement (e.g., both tracks complete)
Then candidates in this stage have independent status per track
And the stage advancement condition evaluates across all tracks
And the UI shows each track's status separately
```

### US-2.07: Configure Mirror View Rules
**As an** Agency Owner (Amir),
**I want to** configure rules that make a candidate appear in multiple views simultaneously,
**So that** parallel processing (e.g., Embassy + LMIS) is supported without duplicating records.

**Acceptance Criteria:**
```gherkin
Given I am configuring a workflow stage
When I add a "mirror view" rule with field conditions
And I specify the target view/stage that should also show this candidate
Then when the conditions are met, the candidate appears in both the source and target views
And the candidate remains a single record (no duplication)
And updates in either view reflect immediately in both
And the mirror rule can be removed or modified without affecting candidate data
```

### US-2.08: Reorder Workflow Stages
**As an** Agency Owner (Amir),
**I want to** reorder stages in my workflow,
**So that** I can adjust the pipeline sequence as my process evolves.

**Acceptance Criteria:**
```gherkin
Given I am on the workflow configuration page
When I drag-and-drop a stage to a new position
Or I manually set a new sort order number
Then the stage order is updated
And existing candidates remain in their current stage (not affected by reorder)
And the workflow visualization reflects the new order
And transition rules are not automatically changed (manual review required)
```

### US-2.09: Configure Mandatory Fields Per Stage
**As an** Agency Owner (Amir),
**I want to** define which fields are mandatory at each stage,
**So that** staff must provide required information before advancing candidates.

**Acceptance Criteria:**
```gherkin
Given I am editing a workflow stage's transition rules
When I mark specific candidate fields as "required for transition"
Then those fields must have values before the transition action button activates
And the UI indicates which fields are missing
And the server rejects transition attempts if required fields are empty
And different transitions from the same stage can have different required fields
```

### US-2.10: Seed Default Workflow Template
**As a** System Admin (Solomon),
**I want to** provision a new agency with the default 8-stage workflow template,
**So that** new agencies can start immediately with a working pipeline configuration.

**Acceptance Criteria:**
```gherkin
Given I am creating a new tenant/agency
When the agency is provisioned
Then the default 8-stage workflow is created automatically:
  | Stage 1: Intake (Registration) |
  | Stage 2: Queue (New Contract View) |
  | Stage 3: Clearances (Embassy View) |
  | Stage 4: Visa Status |
  | Stage 5: Labor Sync (LMIS View) |
  | Stage 6: Logistics (Ticket View) |
  | Stage 7: Countdown (Departure View) |
  | Stage 8: Ground & Fees (Arrival & Commission) |
And all default transition rules, statuses, and action buttons are configured
And the agency owner can modify this template
```

---

## Epic 3: Embassy & Visa Processing (FR-03)

### US-3.01: Transfer Candidate to Embassy View
**As an** Office Manager (Hana),
**I want to** transfer a candidate from the New Contract View to the Embassy View,
**So that** active embassy processing can begin.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate in the New Contract View
And the candidate has all required fields for embassy processing
When I click the "To Embassy" action button
Then the candidate moves to the Embassy View
And the candidate no longer appears in the New Contract View
And a status history entry is created with timestamp and my user ID
And connected users receive a real-time notification via SignalR
And the action is audit-logged
```

### US-3.02: Book Medical Appointment
**As an** Embassy Officer (Dawit),
**I want to** book and track a medical appointment for a candidate,
**So that** the medical clearance track progresses.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate in the Embassy View
When I click "Book Medical"
And I enter the appointment date and facility name
Then the medical track status updates to "Booked"
And the appointment date is recorded
And a notification is sent to relevant parties (field agent, candidate via bot)
And the medical status is displayed alongside the Tasheer status in the Embassy View
```

### US-3.03: Record Medical Result
**As an** Embassy Officer (Dawit),
**I want to** record the medical examination result (Fit or Unfit),
**So that** the medical clearance track is completed.

**Acceptance Criteria:**
```gherkin
Given a candidate has medical status "Booked"
When I update the medical status to "Fit" or "Unfit"
Then the medical track is marked complete (Fit) or flagged (Unfit)
And if Medical = Fit AND Tasheer = Book Done, the candidate automatically appears in the LMIS View
And the candidate remains visible in the Embassy View simultaneously
And connected users see the update in real-time
And the action is audit-logged with result
```

### US-3.04: Book Tasheer Appointment
**As an** Embassy Officer (Dawit),
**I want to** book and track a Tasheer appointment for a candidate,
**So that** the Tasheer clearance track progresses.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate in the Embassy View
When I click "Book Tasheer"
And I enter the appointment date
Then the Tasheer track status updates to "Booked"
And the appointment date is recorded
And both Medical and Tasheer tracks are visible simultaneously
```

### US-3.05: Record Tasheer Result
**As an** Embassy Officer (Dawit),
**I want to** record the Tasheer result (Book Done or Expired),
**So that** the Tasheer clearance track is completed or flagged.

**Acceptance Criteria:**
```gherkin
Given a candidate has Tasheer status "Booked"
When I update the Tasheer status to "Book Done" or "Expired"
Then the Tasheer track is marked complete or expired
And if Medical = Fit AND Tasheer = Book Done, the candidate automatically appears in the LMIS View
And if Expired, a re-booking workflow is triggered
And the action is audit-logged
```

### US-3.06: Mirror View Activation
**As the** System,
**I want to** automatically show a candidate in LMIS View when Medical=Fit AND Tasheer=BookDone,
**So that** parallel processing between Embassy and LMIS can begin.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Embassy View
When Medical status = "Fit" AND Tasheer status = "Book Done"
Then the candidate automatically appears in the LMIS View queries
And the candidate remains fully operational in the Embassy View
And no data duplication occurs (single record, multiple filtered views)
And the mirror activation is logged in status history
And the transition happens within 1 second of the triggering update
```

### US-3.07: Set Operational Status to Ready
**As an** Embassy Officer (Dawit),
**I want to** mark a candidate's operational status as "Ready",
**So that** the Case Executive is notified and can begin documentation processing.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the Embassy View with both clearances complete
When I set the operational status to "Ready"
Then the candidate automatically appears in the Case Executive View
And a real-time notification is sent to assigned Case Executives
And the "Ready" status is reflected in both Embassy and Case Executive views
```

### US-3.08: Process Visa Documentation
**As a** Case Executive (Sara),
**I want to** update the visa documentation status to "Submitted",
**So that** the embassy officer knows physical documents have been submitted.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate in the Case Executive View with status "Ready"
When I update the status to "Submitted"
And I optionally add submission date and reference number
Then the status is updated in both Case Executive and Embassy views
And a notification is sent to the Embassy Officer
And the action is audit-logged
```

### US-3.09: Record Visa Outcome
**As an** Embassy Officer (Dawit),
**I want to** mark the visa status as "Issued" or "Rejected",
**So that** the appropriate next action is triggered.

**Acceptance Criteria:**
```gherkin
Given a candidate has documentation status "Submitted"
When I mark the visa status as "Issued"
Then the "To LMIS" action button appears
And a notification is sent to relevant parties
When I mark the visa status as "Rejected"
Then a rejection reason field becomes required
And the candidate enters a rejection handling workflow
And resubmission options are presented
```

### US-3.10: Transfer to LMIS (Full Transfer)
**As an** Embassy Officer (Dawit),
**I want to** transfer a candidate with "Issued" visa to the LMIS track exclusively,
**So that** the labour registration process begins and the embassy stage is complete.

**Acceptance Criteria:**
```gherkin
Given a candidate has visa status "Issued" in Embassy View
And the "To LMIS" button is visible
When I click "To LMIS"
Then the candidate is removed from the Embassy View
And the candidate is removed from the Case Executive View
And the candidate remains in (or is added to) the LMIS View
And the embassy processing stage is marked as complete in history
And the transition is audit-logged
```

### US-3.11: Handle Visa Rejection
**As an** Embassy Officer (Dawit),
**I want to** manage the resubmission process for rejected visas,
**So that** candidates can retry without starting from scratch.

**Acceptance Criteria:**
```gherkin
Given a candidate has visa status "Rejected"
When I review the rejection reason
And I click "Resubmit"
Then the status resets to preparation phase
And previous rejection details are preserved in history
And the Case Executive is re-notified
And a counter tracks number of resubmission attempts
```

---

## Epic 4: LMIS — Government Labour Registration (FR-04)

### US-4.01: View LMIS Queue
**As an** Office Manager (Hana),
**I want to** view all candidates currently in the LMIS stage,
**So that** I can monitor government registration progress.

**Acceptance Criteria:**
```gherkin
Given I am logged in with LMIS view access
When I navigate to the LMIS View
Then I see all candidates in the LMIS stage for my office
And each record shows: candidate name, insurance status, LMIS milestone, days in stage
And I can sort and filter by insurance status and milestone
And candidates who arrived via mirror view (Medical+Tasheer complete) are included
```

### US-4.02: Record Insurance Payment
**As an** Embassy Officer (Dawit) or Office Manager (Hana),
**I want to** mark a candidate's insurance as Paid,
**So that** the LMIS operational status becomes Available.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the LMIS View with insurance "Unpaid"
When I update insurance status to "Paid"
Then the LMIS operational status automatically changes to "Available"
And the payment date is recorded
And connected users see the update in real-time
And the action is audit-logged
```

### US-4.03: Upload LMIS Documents
**As an** Embassy Officer (Dawit),
**I want to** upload LMIS-related documents for verification,
**So that** the government registration process can proceed.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the LMIS View
When I click "Upload LMIS Document"
And I select a file and document type
Then the document is stored and linked to the candidate's LMIS record
And the document appears in the LMIS document list
And the upload is audit-logged
```

### US-4.04: Track LMIS Milestone Progression
**As an** Embassy Officer (Dawit),
**I want to** update the LMIS milestone status (Uploaded → Check Verified → Issued),
**So that** the sequential progression is tracked.

**Acceptance Criteria:**
```gherkin
Given a candidate is in LMIS View with status "Available"
When I update the milestone to "Uploaded"
Then only "Check Verified" becomes the next valid milestone
When the milestone reaches "Check Verified"
Then only "Issued" becomes the next valid milestone
When the milestone reaches "Issued"
Then the "To Ticket" action button becomes visible
And milestone progression is sequential (cannot skip steps)
And each milestone change is timestamped and audit-logged
```

### US-4.05: Transfer to Ticket View
**As an** Embassy Officer (Dawit),
**I want to** transfer a candidate from LMIS to Ticket View when LMIS status is Issued,
**So that** travel arrangements can begin.

**Acceptance Criteria:**
```gherkin
Given a candidate is in LMIS View with milestone "Issued"
And the "To Ticket" button is visible
When I click "To Ticket"
Then the candidate moves to the Ticket View
And the candidate is removed from the LMIS View
And the transition is logged in status history
And the action is audit-logged
```

---

## Epic 5: Travel & Logistics (FR-05)

### US-5.01: View Ticket Queue
**As an** Office Manager (Hana),
**I want to** view all candidates awaiting travel arrangements,
**So that** I can manage ticket bookings efficiently.

**Acceptance Criteria:**
```gherkin
Given I am logged in with Ticket View access
When I navigate to the Ticket View
Then I see all candidates in the ticketing stage
And each record shows: candidate name, destination, ticket status, flight date (if set)
And I can sort by booking urgency
```

### US-5.02: Book Flight Ticket
**As an** Office Manager (Hana),
**I want to** record flight booking details for a candidate,
**So that** travel logistics are tracked and the departure countdown can begin.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the Ticket View
When I click "Book Ticket"
And I fill in all three required fields: Ticket Book Status, Destination, Flight Date
Then the booking details are saved
And the "To Departure" action button appears (only after all three fields are filled)
And the action is audit-logged
And if any of the three fields is empty, the "To Departure" button remains hidden
```

### US-5.03: Transfer to Departure View
**As an** Office Manager (Hana),
**I want to** transfer a candidate to the Departure View once booking is complete,
**So that** the pre-flight countdown process begins.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Ticket View with all booking fields complete
And the "To Departure" button is visible
When I click "To Departure"
Then the candidate moves to the Departure View
And the departure countdown starts calculating automatically
And the transition is audit-logged
```

### US-5.04: View Departure Countdown
**As an** Office Manager (Hana),
**I want to** see all upcoming departures with countdown timers,
**So that** I can ensure all candidates are notified and prepared.

**Acceptance Criteria:**
```gherkin
Given I am on the Departure View
Then I see all candidates with: Destination, Office Name, Flight Date, Remaining Days
And "Remaining Days" is calculated automatically as (Flight Date - Today)
And candidates are sorted by nearest departure first
And color coding indicates urgency (red: <3 days, yellow: <7 days, green: 7+ days)
```

### US-5.05: Notify Candidate of Departure
**As an** Office Manager (Hana),
**I want to** mark a candidate as "Notified" about their upcoming departure,
**So that** the notification alert is dismissed and I know who still needs notification.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Departure View with Notification Status != "Notified"
Then an alert message displays: "$n$ days left, notify candidate"
When I mark the notification status as "Notified"
Then the alert is hidden for this candidate
And the notification timestamp is recorded
And if a bot channel exists, a push notification is sent to the candidate
```

### US-5.06: Record Successful Departure
**As an** Office Manager (Hana),
**I want to** mark a candidate as "Departed",
**So that** the arrival tracking process can begin.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Departure View
When I select "Departed"
Then the "To Arrival" action button appears
And the departure date/time is recorded
And connected users see the update in real-time
```

### US-5.07: Handle Non-Departure
**As an** Office Manager (Hana),
**I want to** handle cases where a candidate did not depart as scheduled,
**So that** the appropriate corrective action is taken.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Departure View
When I select "Not Departed"
Then the "To Arrival" button is suppressed
And defensive options appear: "Back to Ticket" or "Canceled"
When I select "Back to Ticket"
Then the candidate returns to Ticket View for rebooking
When I select "Canceled"
Then the candidate enters a cancellation workflow
And the reason for non-departure is required
And all actions are audit-logged
```

### US-5.08: Transfer to Arrival View
**As an** Office Manager (Hana),
**I want to** transfer a departed candidate to the Arrival View,
**So that** ground deployment tracking begins.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Departure View with status "Departed"
And the "To Arrival" button is visible
When I click "To Arrival"
Then the candidate moves to the Arrival View
And the transition is audit-logged
```

---

## Epic 6: Arrival & Deployment Tracking (FR-06)

### US-6.01: Confirm Safe Arrival
**As a** Field Agent (Kebede) or Office Manager (Hana),
**I want to** confirm that a candidate has safely arrived at their destination,
**So that** the deployment is tracked and commission processing can begin.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the Arrival View
When I confirm arrival (with optional arrival date and notes)
Then the arrival status is set to "Arrived"
And the "Add to Commission" action button becomes available
And the arrival timestamp is recorded
And the candidate REMAINS in the Arrival View (permanent ledger)
And the action is audit-logged
```

### US-6.02: Flag Exception — Returned
**As an** Office Manager (Hana),
**I want to** flag a candidate as "Returned" if they came back from deployment,
**So that** the exception containment process begins.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the Arrival View
When I flag the status as "Returned"
Then a reason field becomes required
And the candidate is moved to the Exception Containment Workspace
And an investigation record is created with status "Open"
And the agency owner is notified
And financial impact tracking is initiated
And the candidate record remains in Arrival View as historical log
```

### US-6.03: Flag Exception — Runaway
**As an** Office Manager (Hana),
**I want to** flag a candidate as "Runaway" if they disappeared after arrival,
**So that** the exception containment and liability tracking begins.

**Acceptance Criteria:**
```gherkin
Given a candidate is in the Arrival View
When I flag the status as "Runaway"
Then required fields include: last known date, last known location, circumstances
And the candidate is moved to the Exception Containment Workspace
And an investigation record is created with status "Open"
And the agency owner and partner agency are notified
And liability assignment tracking begins
And the candidate record remains in Arrival View as historical log
```

### US-6.04: Exception Containment — Investigation
**As an** Office Manager (Hana),
**I want to** investigate and document exception cases,
**So that** we have a complete record for liability and resolution.

**Acceptance Criteria:**
```gherkin
Given an exception record exists with status "Open"
When I access the Exception Containment Workspace
Then I see: candidate details, exception type, date flagged, investigation notes
And I can add investigation notes with timestamps
And I can attach documents (communications, reports)
And I can assign liability (agency, partner, candidate)
And I can track financial impact (refunds due, fees lost)
```

### US-6.05: Exception Containment — Resolution
**As an** Office Manager (Hana),
**I want to** resolve an exception case and close the investigation,
**So that** financial settlements can be processed.

**Acceptance Criteria:**
```gherkin
Given an exception record is under investigation
When I set the status to "Resolved"
And I provide resolution details (outcome, financial settlement, lessons learned)
Then the exception status moves to "Closed"
And financial entries are created based on the resolution
And the closed case remains accessible for audit
And the agency owner is notified of resolution
```

### US-6.06: Add to Commission
**As an** Office Manager (Hana),
**I want to** transfer a successfully arrived candidate to the Commission View,
**So that** financial ledger tracking begins.

**Acceptance Criteria:**
```gherkin
Given a candidate is in Arrival View with status "Arrived"
And the "Add to Commission" button is visible
When I click "Add to Commission"
Then the candidate record is copied to the Commission View
And the candidate REMAINS in the Arrival View (permanent ledger)
And a commission record is initialized with default fee structure
And the finance team is notified
```

### US-6.07: View Arrival Ledger (Permanent)
**As an** Auditor (Abebe),
**I want to** view the permanent arrival ledger with all historical arrivals,
**So that** I can audit deployment records regardless of current status.

**Acceptance Criteria:**
```gherkin
Given I have Auditor role
When I access the Arrival Ledger
Then I see ALL candidates who ever reached the arrival stage
And records include: arrived, returned, runaway, and their current disposition
And I can filter by date range, office, destination, status
And I can export the ledger to Excel
And viewing this data is read-audit-logged
```

---

## Epic 7: Commission & Finance — Full ERP (FR-07)

### US-7.01: View Commission Queue
**As a** Finance Officer (Yonas),
**I want to** view all candidates with active commission records,
**So that** I can manage fee collection and settlements.

**Acceptance Criteria:**
```gherkin
Given I am logged in as Finance Officer
When I navigate to Commission View
Then I see all candidates with commission records for my office
And each shows: candidate name, total fees, amount paid, balance due, status
And I can filter by: paid/unpaid, office, date range, fee category
And totals are summarized at the top
```

### US-7.02: Record Fee Breakdown
**As a** Finance Officer (Yonas),
**I want to** define the fee breakdown for a candidate's commission,
**So that** each cost component is tracked separately.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's commission record
When I click "Define Fees"
And I enter amounts for: agency fee, government fee, medical fee, ticket cost, insurance, other
And I specify the currency for each (ETB, USD, SAR, AED)
Then the fee breakdown is saved
And the total is automatically calculated
And double-entry journal entries are created (debit receivable, credit revenue per category)
And the breakdown is audit-logged
```

### US-7.03: Record Payment (Double-Entry)
**As a** Finance Officer (Yonas),
**I want to** record a payment received from a candidate or partner,
**So that** the financial ledger is updated with proper double-entry accounting.

**Acceptance Criteria:**
```gherkin
Given I am viewing a candidate's commission record with balance due
When I click "Record Payment"
And I enter: amount, currency, payment method, reference number, date, payer
Then a journal entry is created: debit cash/bank, credit receivable
And the candidate's balance due is reduced
And if fully paid, the commission status is updated to "Settled"
And a payment receipt can be generated
And the transaction is audit-logged
```

### US-7.04: Multi-Currency Support
**As a** Finance Officer (Yonas),
**I want to** handle transactions in multiple currencies,
**So that** fees charged in SAR/USD and payments received in ETB are properly tracked.

**Acceptance Criteria:**
```gherkin
Given a commission record has fees in multiple currencies
When I record a payment in a different currency than the fee
Then I am prompted to enter the exchange rate
And the system calculates the equivalent amount
And exchange rate differences are recorded as gain/loss journal entries
And all amounts are stored with their original currency
And reports can show amounts in original or converted currency
```

### US-7.05: Bank Reconciliation
**As a** Finance Officer (Yonas),
**I want to** reconcile bank statements against recorded payments,
**So that** discrepancies are identified and resolved.

**Acceptance Criteria:**
```gherkin
Given I am on the Bank Reconciliation page
When I upload or enter bank statement transactions
Then the system attempts to auto-match with recorded payments (by amount, date, reference)
And matched transactions are highlighted
And unmatched bank entries and unmatched system payments are listed separately
And I can manually match or create adjustment entries
And the reconciliation is saved with date and operator
```

### US-7.06: Generate Financial Statements
**As a** Finance Officer (Yonas) or Agency Owner (Amir),
**I want to** generate Profit & Loss and Balance Sheet reports,
**So that** the agency's financial health is visible.

**Acceptance Criteria:**
```gherkin
Given I have Finance or Agency Owner role
When I request a P&L statement for a date range
Then the system generates revenue (commission income), expenses, and net profit
When I request a Balance Sheet as of a date
Then the system shows assets (receivables, cash), liabilities, and equity
And reports can be filtered by office/branch
And reports can be exported to PDF and Excel
And generating reports is audit-logged
```

### US-7.07: Track Disputes
**As a** Finance Officer (Yonas),
**I want to** log and track financial disputes with candidates or partners,
**So that** contested amounts are managed and resolved.

**Acceptance Criteria:**
```gherkin
Given a commission record exists
When I click "Log Dispute"
And I enter: disputed amount, reason, counterparty, supporting documents
Then a dispute record is created with status "Open"
And the disputed amount is flagged in the candidate's balance
And I can add resolution notes and track progress
And when resolved, I can record the outcome (adjusted, waived, enforced)
And financial entries are created based on resolution
```

### US-7.08: Per-Office Commission Reporting
**As an** Agency Owner (Amir),
**I want to** compare commission performance across my offices,
**So that** I can identify high and low performing branches.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner with multiple offices
When I navigate to Commission Reports
Then I see: total candidates per office, total fees, collected, outstanding, collection rate
And I can compare offices side-by-side
And I can drill down into individual office details
And the report updates in real-time as payments are recorded
```

### US-7.09: Tax Calculations
**As a** Finance Officer (Yonas),
**I want to** apply tax calculations to fees and generate tax reports,
**So that** the agency complies with tax obligations.

**Acceptance Criteria:**
```gherkin
Given tax rates are configured in system settings
When a fee is recorded
Then applicable taxes are automatically calculated
And tax amounts are stored as separate journal entries
And I can generate tax summary reports by period
And tax configuration can be updated by admin
```

---

## Epic 8: Agency ERP (FR-08)

### US-8.01: Manage Staff/Employees
**As an** Office Manager (Hana) or Admin (Solomon),
**I want to** manage staff records for my office,
**So that** employee information is centralized and up-to-date.

**Acceptance Criteria:**
```gherkin
Given I have staff management permission
When I navigate to Staff Management
Then I see all staff in my scope (office for Office Manager, all for Admin)
And I can add new staff with: name, role, office, contact, start date
And I can edit existing staff details
And I can deactivate (suspend/terminate) staff
And staff changes are audit-logged
```

### US-8.02: Manage Offices/Branches
**As an** Agency Owner (Amir),
**I want to** manage my agency's offices and branches,
**So that** each physical location is tracked and candidates can be assigned.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner
When I navigate to Office Management
Then I see all offices for my agency
And I can add new offices with: name, address, city, country, phone, manager
And I can edit or deactivate offices
And offices are used as filter criteria throughout the system
```

### US-8.03: Manage Partner Agencies
**As an** Agency Owner (Amir),
**I want to** maintain a directory of overseas partner agencies and employers,
**So that** candidates can be linked to their destination employer.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner or Office Manager
When I navigate to Partner Directory
Then I see all partner agencies/employers for my agency
And I can add: company name, country, contact person, phone, email, address
And partners appear in candidate registration as "Office Name" options
And I can view all candidates linked to a specific partner
```

### US-8.04: Configure Roles and Permissions
**As a** System Admin (Solomon),
**I want to** manage roles and assign granular permissions,
**So that** each user has exactly the access they need.

**Acceptance Criteria:**
```gherkin
Given I am System Admin
When I navigate to Role Management
Then I see all roles with their assigned permissions
And I can create new roles with selected permissions
And I can edit existing roles
And permissions are granular: module.action (e.g., candidate.create, commission.view, workflow.configure)
And role changes take effect immediately for logged-in users
And role modifications are audit-logged
```

### US-8.05: View Audit Trail
**As an** Auditor (Abebe),
**I want to** view the complete audit trail of all system operations,
**So that** I can verify compliance and investigate issues.

**Acceptance Criteria:**
```gherkin
Given I am Auditor
When I navigate to Audit Trail
Then I see all logged operations: user, action, entity, timestamp, before/after values
And I can filter by: user, action type, entity, date range
And I can export filtered results to Excel
And the audit trail includes both write operations and read access logs
And viewing audit data does not create recursive audit entries
```

### US-8.06: Dashboard and KPIs
**As an** Agency Owner (Amir) or Office Manager (Hana),
**I want to** see a dashboard with key performance indicators,
**So that** I have real-time visibility into business operations.

**Acceptance Criteria:**
```gherkin
Given I am logged in with dashboard access
When I navigate to the Overview/Dashboard
Then I see KPIs: total candidates, candidates per stage, this month's departures, revenue this month
And I see charts: pipeline funnel, monthly trend, top destinations
And data reflects my scope (agency-wide for owner, office-specific for manager)
And dashboard data refreshes via real-time updates (SignalR)
And I can click KPI cards to drill into detailed views
```

### US-8.07: Provision New Tenant/Agency
**As a** System Admin (Solomon),
**I want to** create a new tenant (agency) in the system,
**So that** a new agency can start using the platform.

**Acceptance Criteria:**
```gherkin
Given I am System Admin
When I click "Create Agency"
And I fill in: agency name, contact details, admin user details
Then a new PostgreSQL schema is created for this agency
And the default workflow template is seeded
And default roles and permissions are created
And an Agency Owner user account is provisioned
And the tenant is immediately accessible
And the provisioning is audit-logged
```

---

## Epic 9: Telegram/WhatsApp Bot (FR-09)

### US-9.01: Bot Infrastructure — Telegram Connection
**As a** Notification Manager (Meron),
**I want to** configure the Telegram bot connection,
**So that** field agents can interact with the system via Telegram.

**Acceptance Criteria:**
```gherkin
Given I am Notification Manager
When I navigate to Bot Configuration
And I enter the Telegram Bot Token
And I click "Connect"
Then the system establishes a connection with the Telegram Bot API
And the connection status is displayed (Connected/Disconnected)
And the bot responds to a test message
And the configuration is stored securely (encrypted)
```

### US-9.02: Bot Infrastructure — WhatsApp Connection
**As a** Notification Manager (Meron),
**I want to** configure the WhatsApp Business API connection,
**So that** field agents can interact with the system via WhatsApp.

**Acceptance Criteria:**
```gherkin
Given I am Notification Manager
When I navigate to Bot Configuration
And I enter the WhatsApp Business API credentials
And I click "Connect"
Then the system establishes a connection with the WhatsApp API
And the connection status is displayed
And the configuration is stored securely
```

### US-9.03: Status Lookup via Bot
**As a** Field Agent (Kebede),
**I want to** look up a candidate's current status via Telegram/WhatsApp,
**So that** I can provide information without accessing the web system.

**Acceptance Criteria:**
```gherkin
Given I am a registered field agent with bot access
When I send a message: "/status [passport_number]" or "/status [candidate_name]"
Then the bot responds with: candidate name, current stage, current status, last update date
And the response is in my preferred language (Amharic or English)
And the lookup is audit-logged
And response time is under 3 seconds
And if no candidate is found, a helpful message is returned
```

### US-9.04: Push Notifications for Stage Transitions
**As a** Field Agent (Kebede),
**I want to** receive automatic notifications when candidates I'm tracking move to a new stage,
**So that** I can take timely action without checking the system.

**Acceptance Criteria:**
```gherkin
Given I am subscribed to notifications for specific candidates or my office
When a candidate transitions to a new stage
Then I receive a push notification via my configured channel (Telegram/WhatsApp)
And the notification includes: candidate name, new stage, required action (if any)
And the notification is in my preferred language
And notifications are delivered within 30 seconds of the transition
```

### US-9.05: Quick Action — Update Medical Status
**As a** Field Agent (Kebede),
**I want to** update a candidate's medical status via bot,
**So that** I can report results immediately from the medical facility.

**Acceptance Criteria:**
```gherkin
Given I am a field agent with update permissions
When I send: "/medical [passport_number] fit" or "/medical [passport_number] unfit"
Then the system updates the candidate's medical status
And the same mirror view logic triggers as the web interface
And a confirmation message is returned
And the action is audit-logged with my user ID
And the server validates my permission before executing
```

### US-9.06: Quick Action — Confirm Arrival
**As a** Field Agent (Kebede),
**I want to** confirm a candidate's arrival via bot,
**So that** the arrival is recorded immediately from the destination.

**Acceptance Criteria:**
```gherkin
Given I am a field agent with arrival confirmation permission
When I send: "/arrived [passport_number]"
Then the system confirms the candidate's arrival
And the same workflow logic triggers as the web interface
And a confirmation message is returned
And the action is audit-logged
```

### US-9.07: CV Generation via Bot
**As a** Field Agent (Kebede),
**I want to** request a candidate's CV via bot,
**So that** I can share it with partners immediately.

**Acceptance Criteria:**
```gherkin
Given I am a field agent
When I send: "/cv [passport_number]"
Then the system generates a CV PDF for the candidate
And the PDF is sent as a file attachment in the bot conversation
And the generation is logged
And response time is under 10 seconds
```

### US-9.08: Multi-Language Bot Responses
**As a** Field Agent (Kebede),
**I want to** interact with the bot in Amharic,
**So that** I can use the system comfortably in my primary language.

**Acceptance Criteria:**
```gherkin
Given I have set my language preference to Amharic
When I send any bot command
Then all responses (status, notifications, confirmations, errors) are in Amharic
And I can switch language with: "/lang am" or "/lang en"
And the language preference is saved for future interactions
```

### US-9.09: Bot User Registration
**As a** Notification Manager (Meron),
**I want to** link bot users to system user accounts,
**So that** bot interactions are authenticated and authorized.

**Acceptance Criteria:**
```gherkin
Given a field agent wants to use the bot
When they send "/register" to the bot
Then the bot asks for their system username
And the bot sends a verification code to their registered system email
And once verified, their Telegram/WhatsApp ID is linked to their system account
And all subsequent bot commands are executed with their system permissions
```

---

## Epic 10: Reporting & Analytics (FR-10)

### US-10.01: Pipeline View Report
**As an** Agency Owner (Amir) or Office Manager (Hana),
**I want to** see how many candidates are in each pipeline stage,
**So that** I can identify bottlenecks and capacity issues.

**Acceptance Criteria:**
```gherkin
Given I have reporting access
When I navigate to Pipeline Report
Then I see a funnel/bar chart showing candidate count per stage
And I can filter by: office, date range, destination country
And I can click each stage to see the list of candidates
And counts update in real-time
```

### US-10.02: Agency Performance Dashboard
**As an** Agency Owner (Amir),
**I want to** see overall agency performance metrics,
**So that** I can assess business health at a glance.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner
When I view the Performance Dashboard
Then I see: total processed this month/quarter/year, average processing time per stage, success rate, revenue
And I see trend charts comparing current vs previous periods
And I can filter by office and destination
```

### US-10.03: Office Comparison Report
**As an** Agency Owner (Amir),
**I want to** compare performance metrics across my offices,
**So that** I can identify high and low performing branches.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner with multiple offices
When I navigate to Office Comparison
Then I see a table/chart comparing: candidates processed, average time, revenue, success rate
And offices are ranked by configurable metrics
And I can drill down into any office for details
```

### US-10.04: Overdue/Stuck Candidates Alert
**As an** Office Manager (Hana),
**I want to** see candidates who have been stuck in a stage beyond expected timeframes,
**So that** I can intervene and resolve delays.

**Acceptance Criteria:**
```gherkin
Given configurable time thresholds exist per stage (e.g., Embassy > 30 days)
When a candidate exceeds the threshold
Then they appear in the "Overdue" alerts list
And an automatic notification is sent to the Office Manager
And the alert shows: candidate name, stage, days stuck, last action date
And I can take action directly from the alert (view candidate, reassign)
```

### US-10.05: Export to Excel
**As an** Auditor (Abebe) or Office Manager (Hana),
**I want to** export report data to Excel,
**So that** I can perform additional analysis or share with stakeholders.

**Acceptance Criteria:**
```gherkin
Given I am viewing any report with data
When I click "Export to Excel"
Then an .xlsx file is generated with the current filtered dataset
And column headers match the displayed table columns
And the file downloads automatically
And the export action is audit-logged
```

### US-10.06: Export to PDF
**As a** Finance Officer (Yonas) or Agency Owner (Amir),
**I want to** generate PDF reports for printing or formal sharing,
**So that** I can provide professional documents to stakeholders.

**Acceptance Criteria:**
```gherkin
Given I am viewing a report
When I click "Export to PDF"
Then a formatted PDF is generated with agency branding/header
And the PDF includes the report title, date range, filters applied, and data
And the file downloads automatically
```

### US-10.07: Scheduled Reports
**As an** Agency Owner (Amir),
**I want to** configure reports to be generated and emailed automatically,
**So that** I receive key metrics without logging in.

**Acceptance Criteria:**
```gherkin
Given I am Agency Owner or Office Manager
When I navigate to "Scheduled Reports"
And I configure: report type, frequency (daily/weekly/monthly), recipients, filters
Then the system generates the report on schedule
And the report is emailed as PDF attachment to configured recipients
And I can view scheduled report history (last run, status)
And I can pause or delete scheduled reports
```

---

## Epic 11: Cross-Cutting Concerns

### US-11.01: User Login
**As any** system user,
**I want to** log in with my credentials,
**So that** I can access the system securely.

**Acceptance Criteria:**
```gherkin
Given I have valid credentials
When I enter username and password on the login page
And I click "Login"
Then I receive a JWT access token and refresh token
And I am redirected to my role-appropriate dashboard
And my login is logged (IP, timestamp, user agent)
And failed logins trigger lockout after 5 attempts
And the response time is under 1 second
```

### US-11.02: Multi-Factor Authentication
**As an** Admin (Solomon) or Agency Owner (Amir),
**I want to** enable MFA for my account,
**So that** my account is protected against credential theft.

**Acceptance Criteria:**
```gherkin
Given I have MFA setup permission
When I navigate to Security Settings and click "Enable MFA"
Then I am shown a QR code for TOTP authenticator app
And I enter a verification code to confirm setup
Then on subsequent logins, I must provide the TOTP code after password
And MFA can be required by admin for specific roles
```

### US-11.03: Real-Time Notifications (SignalR)
**As any** authenticated user,
**I want to** receive real-time updates when candidates in my scope change stage,
**So that** I see current data without refreshing the page.

**Acceptance Criteria:**
```gherkin
Given I am logged in and connected via SignalR
When a candidate in my scope (my office, my assigned candidates) transitions to a new stage
Then I receive a real-time notification in the UI
And the candidate's status updates in any open views without page refresh
And if my connection drops, updates are received on reconnection
And notifications do not interrupt active work (non-intrusive toast)
```

### US-11.04: Tenant Data Isolation
**As any** user belonging to an agency,
**I want to** see only my agency's data,
**So that** other agencies' data is never accessible to me.

**Acceptance Criteria:**
```gherkin
Given I am logged in as a user belonging to Agency A
When I make any API request
Then the system routes to my agency's PostgreSQL schema
And I cannot access data from Agency B's schema under any circumstances
And system admins can access any agency's schema for administration
And tenant isolation is enforced at the database layer, not just application layer
```

### US-11.05: Password Change and Expiry
**As any** system user,
**I want to** change my password and be notified when it expires,
**So that** my account remains secure.

**Acceptance Criteria:**
```gherkin
Given my password is approaching expiry (90-day policy)
Then I am notified 7 days before expiry
When I change my password
Then the new password must meet complexity requirements
And I cannot reuse my last 5 passwords
And the change is audit-logged
And if password is expired, I am forced to change on next login
```

### US-11.06: Session Management
**As a** System Admin (Solomon),
**I want to** view and terminate active user sessions,
**So that** I can respond to security incidents.

**Acceptance Criteria:**
```gherkin
Given I am System Admin
When I navigate to Active Sessions
Then I see all active sessions: user, IP, device, login time, last activity
And I can terminate individual sessions
And I can terminate all sessions for a specific user
And terminated sessions are immediately invalidated
```

---
