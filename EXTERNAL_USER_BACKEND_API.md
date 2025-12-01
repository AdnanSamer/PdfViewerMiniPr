# External User Backend API Documentation

## Overview

This document describes the backend API endpoints for external users to review and approve PDF workflows. External users access the system via a secure token sent via email.

---

## Base URL

```
https://localhost:7121/api/external-review
```

---

## Authentication

External users authenticate using a **token** sent via email. The token is passed as a query parameter or in the request body, depending on the endpoint.

**Token Validation:**
- Tokens expire after 24 hours (configurable)
- Tokens can be reused for multiple workflows from the same external reviewer
- Tokens are validated on every request

---

## API Endpoints

### 1. Get External User Info

**Endpoint:** `GET /api/external-review/user-info?token={token}`

**Purpose:** Get the external user's email address from the token. Use this to display the user's name/email in the header.

**Request:**
- Query Parameter: `token` (string, required)

**Response (200 OK):**
```json
{
  "email": "external@example.com",
  "isValid": true
}
```

**Response (400 Bad Request):**
```json
{
  "error": "Token is required."
}
```

**Response (404 Not Found):**
```json
{
  "error": "Invalid or expired token."
}
```

**Usage Example:**
```typescript
// Angular: Get user info for header display
const userInfo = await this.http.get<ExternalUserInfoDto>(
  `${this.apiUrl}/external-review/user-info?token=${token}`
).toPromise();

// Display: userInfo.email in header instead of "InternalUser"
```

---

### 2. Get All Workflows for External User

**Endpoint:** `GET /api/external-review/workflows?token={token}`

**Purpose:** Retrieve all workflows assigned to an external user by their authentication token. Returns all workflows regardless of status (frontend filters into Pending/Approved).

**Request:**
- Query Parameter: `token` (string, required)

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "title": "Contract Review - Q1 2024",
    "status": 3,
    "internalReviewerName": "John Doe",
    "externalReviewerEmail": "external@example.com",
    "pdfFileName": "workflow-1.pdf",
    "pdfFilePath": "Uploads/workflow-1.pdf"
  },
  {
    "id": 2,
    "title": "Agreement Review - Project Alpha",
    "status": 4,
    "internalReviewerName": "Jane Smith",
    "externalReviewerEmail": "external@example.com",
    "pdfFileName": "workflow-2.pdf",
    "pdfFilePath": "Uploads/workflow-2.pdf"
  }
]
```

**Status Codes:**
- `0` = Draft
- `1` = PendingInternalReview
- `2` = InternalApproved
- `3` = PendingExternalReview (show in "Pending PDFs" section)
- `4` = Completed/Approved (show in "Approved PDFs" section)
- `5` = Rejected

**Error Responses:**

**400 Bad Request:**
```json
{
  "error": "Token is required."
}
```

**401 Unauthorized:**
```json
{
  "error": "Token has expired."
}
```

**404 Not Found:**
```json
{
  "error": "Token not found."
}
```
or
```json
{
  "error": "No workflows found for this token."
}
```

---

### 3. Get Single Workflow

**Endpoint:** `GET /api/external-review/workflow?token={token}&workflowId={id}`

**Purpose:** Get a specific workflow by ID. Used when opening a PDF for viewing.

**Request:**
- Query Parameter: `token` (string, required)
- Query Parameter: `workflowId` (int, optional) - If not provided, returns the workflow associated with the token

**Response (200 OK):**
```json
{
  "id": 1,
  "title": "Contract Review - Q1 2024",
  "status": 3,
  "internalReviewerName": "John Doe",
  "externalReviewerEmail": "external@example.com",
  "pdfFileName": "workflow-1.pdf",
  "pdfFilePath": "Uploads/workflow-1.pdf"
}
```

**Response (404 Not Found):**
```json
{
  "error": "Invalid or expired token, or workflow not found."
}
```

---

### 4. Validate OTP

**Endpoint:** `POST /api/external-review/validate-otp`

**Purpose:** Validate the OTP code sent to the external user via email.

**Request Body:**
```json
{
  "token": "abc123xyz",
  "otp": "123456"
}
```

**Response (200 OK):**
```json
true
```
or
```json
false
```

**Usage:** Call this when the user first accesses the dashboard to verify their OTP.

---

### 5. Approve Workflow (with Optional Stamp)

**Endpoint:** `POST /api/external-review/approve`

**Purpose:** Approve a workflow and optionally add a stamp to the PDF. Changes workflow status from `PendingExternalReview` (3) to `Completed` (4).

**Request Body:**
```json
{
  "token": "abc123xyz",
  "workflowId": 1,
  "stamp": {
    "label": "Approved",
    "pageNumber": 1,
    "x": 100.5,
    "y": 200.5
  }
}
```

**Note:** 
- `workflowId` is **optional**. If not provided, approves the workflow associated with the token.
- `stamp` is **optional**. If not provided, just approves the workflow without adding a stamp.

**Minimal Request (approve without stamp):**
```json
{
  "token": "abc123xyz",
  "workflowId": 1
}
```

**Response (200 OK):**
```json
{
  "message": "Workflow approved successfully."
}
```

**Error Responses:**

**400 Bad Request:**
```json
{
  "error": "Invalid token."
}
```
or
```json
{
  "error": "Token expired."
}
```
or
```json
{
  "error": "Workflow not found."
}
```
or
```json
{
  "error": "Workflow does not belong to this external reviewer."
}
```
or
```json
{
  "error": "Workflow is not pending external review."
}
```

**Business Logic:**
1. Validates token exists and is not expired
2. Validates workflow exists and belongs to the external reviewer
3. Validates workflow status is `PendingExternalReview` (3)
4. If stamp is provided:
   - Saves stamp to database
   - Applies stamp to PDF file
5. Updates workflow status to `Completed` (4)
6. Sets `ExternalApprovedAtUtc` timestamp

---

## Frontend Integration Guide

### 1. Display External User Email in Header

**Problem:** Header shows "InternalUser" but should show external user's email.

**Solution:**
```typescript
// On component load (external-review-dashboard.component.ts)
ngOnInit() {
  const token = this.route.snapshot.queryParams['token'];
  
  // Get user info
  this.http.get<ExternalUserInfoDto>(
    `${this.apiUrl}/external-review/user-info?token=${token}`
  ).subscribe({
    next: (userInfo) => {
      this.currentUser = userInfo.email; // Display this in header
    },
    error: (err) => {
      console.error('Failed to get user info', err);
    }
  });
}
```

**Template:**
```html
<!-- Remove "Create Workflow" and "Assigned Reviews" links for external users -->
<div *ngIf="!isExternalUser">
  <a routerLink="/create-workflow">Create Workflow</a>
  <a routerLink="/assigned-reviews">Assigned Reviews</a>
</div>

<!-- Show external user email -->
<div *ngIf="isExternalUser">
  <span>{{ currentUser }}</span>
  <button (click)="logout()">Logout</button>
</div>
```

---

### 2. Enable PDF Modification for Pending Workflows

**Problem:** External users should be able to modify PDFs (add stamps) when status is `PendingExternalReview` (3).

**Solution:**
```typescript
// In PDF viewer component
canModifyPdf(): boolean {
  return this.workflow.status === 3; // PendingExternalReview
}

// Enable annotations/stamps only if canModifyPdf() returns true
<pdf-viewer
  [enableAnnotations]="canModifyPdf()"
  [enableFormFields]="canModifyPdf()"
  ...
></pdf-viewer>
```

---

### 3. Approve Button Implementation

**Problem:** Add an "Approve" button that changes workflow status to `Completed`.

**Solution:**
```typescript
// In external-review-viewer.component.ts
approveWorkflow() {
  const token = this.route.snapshot.queryParams['token'];
  const workflowId = this.workflow.id;
  
  // Get stamp data from PDF viewer if user added stamps
  const stamp = this.getStampFromViewer(); // Your implementation
  
  const approvalDto: ExternalApprovalDto = {
    token: token,
    workflowId: workflowId,
    stamp: stamp // Optional: include if user added stamps
  };
  
  this.http.post(
    `${this.apiUrl}/external-review/approve`,
    approvalDto
  ).subscribe({
    next: () => {
      // Success: Update UI, show message, redirect to dashboard
      this.workflow.status = 4; // Completed
      this.showSuccessMessage('Workflow approved successfully');
      this.router.navigate(['/external-review/dashboard'], {
        queryParams: { token: token }
      });
    },
    error: (err) => {
      // Show error message
      this.showErrorMessage(err.error?.error || 'Failed to approve workflow');
    }
  });
}
```

**Template:**
```html
<!-- Show approve button only for pending workflows -->
<button 
  *ngIf="workflow.status === 3"
  (click)="approveWorkflow()"
  class="btn btn-success">
  Approve
</button>
```

---

### 4. Filter Workflows by Status

**Problem:** Display workflows in "Pending PDFs" and "Approved PDFs" sections.

**Solution:**
```typescript
// In external-review-dashboard.component.ts
getPendingWorkflows(): WorkflowSummaryDto[] {
  return this.workflows.filter(w => w.status === 3); // PendingExternalReview
}

getApprovedWorkflows(): WorkflowSummaryDto[] {
  return this.workflows.filter(w => w.status === 4); // Completed
}
```

**Template:**
```html
<!-- Pending PDFs Section -->
<div class="pending-section">
  <h3>Pending PDFs ({{ getPendingWorkflows().length }})</h3>
  <table>
    <tr *ngFor="let workflow of getPendingWorkflows()">
      <td>{{ workflow.title }}</td>
      <td>{{ workflow.status }}</td>
      <td>
        <button (click)="viewPdf(workflow.id)">View PDF</button>
      </td>
    </tr>
  </table>
</div>

<!-- Approved PDFs Section -->
<div class="approved-section">
  <h3>Approved PDFs ({{ getApprovedWorkflows().length }})</h3>
  <table>
    <tr *ngFor="let workflow of getApprovedWorkflows()">
      <td>{{ workflow.title }}</td>
      <td>{{ workflow.status }}</td>
      <td>
        <button (click)="viewPdf(workflow.id)">View PDF</button>
      </td>
    </tr>
  </table>
</div>
```

---

## Data Transfer Objects (DTOs)

### ExternalUserInfoDto
```typescript
interface ExternalUserInfoDto {
  email: string;
  isValid: boolean;
}
```

### WorkflowSummaryDto
```typescript
interface WorkflowSummaryDto {
  id: number;
  title: string;
  status: number; // 0-5 (see status codes above)
  internalReviewerName: string;
  externalReviewerEmail: string;
  pdfFileName: string;
  pdfFilePath: string;
}
```

### ExternalApprovalDto
```typescript
interface ExternalApprovalDto {
  token: string;
  workflowId?: number; // Optional
  stamp?: StampDto; // Optional
}

interface StampDto {
  label: string;
  pageNumber: number;
  x: number;
  y: number;
}
```

---

## Error Handling

All endpoints return appropriate HTTP status codes:

- **200 OK** - Success
- **400 Bad Request** - Invalid request (missing token, invalid format, etc.)
- **401 Unauthorized** - Token expired
- **404 Not Found** - Token not found, workflow not found, etc.

Error responses follow this format:
```json
{
  "error": "Error message here"
}
```

---

## Security Considerations

1. **Token Validation:** All endpoints validate the token before processing requests
2. **Workflow Ownership:** The system ensures external users can only access workflows assigned to their email
3. **Status Validation:** Approval only works for workflows in `PendingExternalReview` status
4. **Token Expiration:** Tokens expire after 24 hours (configurable)

---

## Testing Checklist

- [ ] Get user info endpoint returns correct email
- [ ] Get all workflows returns all workflows for external reviewer
- [ ] Get single workflow works with workflowId parameter
- [ ] Approve endpoint changes status from 3 to 4
- [ ] Approve endpoint applies stamp if provided
- [ ] Approve endpoint works without stamp (just approval)
- [ ] Error handling works for invalid/expired tokens
- [ ] Error handling works for workflows not belonging to external reviewer
- [ ] Error handling works for workflows not in pending status

---

## Summary of Changes

### New Endpoint:
- `GET /api/external-review/user-info?token={token}` - Get external user email

### Updated Endpoint:
- `POST /api/external-review/approve` - Now supports optional `workflowId` and optional `stamp`

### Updated DTOs:
- `ExternalApprovalDto` - Added optional `workflowId` and made `stamp` optional
- `ExternalUserInfoDto` - New DTO for user info

---

**End of Documentation**

