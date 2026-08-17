# Frontend Components — Unit 2: Candidate & Workflow Engine

## Pages

### Candidate List Page (`/candidates`)
- **Component**: `CandidateListPage`
- **Permission**: `candidate.read`
- **Features**:
  - Data table with columns: Name, Passport, Current Stage, Status, Office, Country, Registered Date
  - Search bar (name, passport, labour ID)
  - Filters: stage, status, office, country of travel
  - "Register New" button (requires `candidate.create`)
  - Click row → navigate to candidate detail
  - Real-time updates via SignalR (new registrations, stage changes)

### Candidate Detail Page (`/candidates/[id]`)
- **Component**: `CandidateDetailPage`
- **Permission**: `candidate.read`
- **Tabs**:
  - **Profile** — biographical data, edit button
  - **Documents** — upload, list, preview, download
  - **Timeline** — workflow event history (chronological)
  - **Actions** — available workflow actions (dynamic buttons)
- **Real-time**: Live updates when other users modify this candidate

### Candidate Registration Form
- **Component**: `RegisterCandidateForm`
- **Permission**: `candidate.create`
- **Fields**: First name, Last name, Middle name, Passport number, DOB, Gender, Nationality, Phone, Email, Address, City, Country, Labour ID, Country of Travel, **Partner Agency** (AppSheet Office; free-text until Unit 6 catalog+links), **Registering Branch** (`officeId`), Contract Date
- **Validation**: Zod schema matching backend rules
- **Behavior**: On submit → POST /api/candidates → navigate to detail page
- **data-testid**: `register-candidate-form`, `register-candidate-submit-button`

### Workflow View Page (`/workflow/[stageId]`)
- **Component**: `WorkflowViewPage`
- **Permission**: `workflow.view`
- **Features**:
  - Stage name as page title
  - Data table: candidates currently in this stage (including mirror views)
  - Stage-specific columns (e.g., Embassy shows Medical + Tasheer columns)
  - Action buttons per row (server-provided, client-optimistic)
  - Filters: status within stage, office
  - Real-time: candidates appear/disappear as they transition

### Workflow Configuration Page (`/admin/workflow`)
- **Component**: `WorkflowConfigPage`
- **Permission**: `workflow.configure`
- **Features**:
  - Visual pipeline view (stages in order, drag-to-reorder)
  - Click stage → edit panel (name, type, statuses, parallel tracks)
  - Transition rule builder (source → target, conditions, button label, roles)
  - Mirror view rule builder (conditions → target stage)
  - Mandatory field configuration per stage/transition
  - "Preview" mode — see which buttons would appear for test data

---

## Shared Components

### ActionButtonBar
- **Purpose**: Render dynamic action buttons for a candidate
- **Props**: `candidateId: string`, `actions: ActionDto[]`
- **Behavior**:
  - Render each action as a button
  - Enabled/disabled based on `isEnabled` flag
  - Tooltip shows `disabledReason` when disabled
  - On click: confirm dialog → execute transition → optimistic update
  - data-testid: `action-button-{transitionRuleId}`

### CandidateStatusBadge
- **Purpose**: Display current stage + status as colored badge
- **Props**: `stageName: string`, `statusValues: Record<string, string>`
- **Behavior**: Render stage name with status chips for parallel tracks

### WorkflowTimeline
- **Purpose**: Display chronological event history for a candidate
- **Props**: `events: WorkflowEventDto[]`
- **Behavior**:
  - Vertical timeline layout
  - Each event shows: type icon, description, user, timestamp
  - Expandable detail for event data (JSON)
  - Most recent first (reverse chronological)

### DocumentUploader
- **Purpose**: Upload files for a candidate
- **Props**: `candidateId: string`, `onUpload: (doc) => void`
- **Behavior**:
  - Drag-and-drop zone + file picker
  - Document type selector (dropdown)
  - Size validation (client-side < 10MB)
  - Type validation (client-side extension check)
  - Upload progress indicator
  - data-testid: `document-uploader`, `document-type-select`

### DocumentList
- **Purpose**: Display and manage candidate documents
- **Props**: `documents: DocumentDto[]`
- **Behavior**:
  - Grid/list view toggle
  - Thumbnail preview for images
  - Click to download or preview inline (PDF viewer)
  - Delete button (soft delete, requires permission)
  - Group by document type

### ConditionBuilder (Admin)
- **Purpose**: Visual condition rule editor for workflow configuration
- **Props**: `conditions: ConditionJson`, `onChange: (conditions) => void`
- **Behavior**:
  - AND/OR group selector
  - Add rule: field dropdown → operator → value input
  - Remove rule button
  - Nested groups support
  - Live preview of condition as human-readable text

---

## State Management (Zustand)

### candidateStore
```typescript
interface CandidateStore {
  candidates: CandidateListDto[];
  totalCount: number;
  filters: CandidateFilters;
  setFilters: (filters: Partial<CandidateFilters>) => void;
  // SWR handles data fetching; store manages filter state
}
```

### workflowStore
```typescript
interface WorkflowStore {
  definition: WorkflowDefinitionDto | null;
  currentStageId: string | null;
  setCurrentStage: (stageId: string) => void;
}
```

## API Integration Points

| Component | API Endpoint | Method |
|-----------|-------------|--------|
| CandidateListPage | /api/candidates | GET (paginated, filtered) |
| RegisterCandidateForm | /api/candidates | POST |
| CandidateDetailPage | /api/candidates/{id} | GET |
| CandidateDetailPage (edit) | /api/candidates/{id} | PUT |
| DocumentUploader | /api/candidates/{id}/documents | POST (multipart) |
| DocumentList | /api/candidates/{id}/documents | GET |
| WorkflowTimeline | /api/workflow/{candidateId}/events | GET |
| ActionButtonBar | /api/workflow/{candidateId}/actions | GET |
| ActionButtonBar (execute) | /api/workflow/{candidateId}/transition | POST |
| WorkflowViewPage | /api/workflow/views/{stageId}/candidates | GET |
| WorkflowConfigPage | /api/workflow/definition | GET/PUT |
