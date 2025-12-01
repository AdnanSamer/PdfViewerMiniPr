# 🅰️ Angular Frontend Implementation Guide

## 📋 **Complete Guide for Building the Angular Frontend**

This guide provides everything needed to build the Angular frontend for the PDF Workflow Management System.

---

## 🚀 **Step 1: Project Setup**

### 1.1 Create Angular Project
```bash
ng new pdf-viewer-workflow --routing --style=scss
cd pdf-viewer-workflow
```

### 1.2 Install Required Packages
```bash
# Syncfusion PDF Viewer
npm install @syncfusion/ej2-angular-pdfviewer --save
npm install @syncfusion/ej2-base @syncfusion/ej2-pdfviewer --save

# PrimeNG (for UI components)
npm install primeng primeicons --save

# HTTP Client (already included, but ensure it's imported)
# Forms (already included)
```

### 1.3 Environment Configuration

**`src/environments/environment.ts`:**
```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7121'
};
```

**`src/environments/environment.prod.ts`:**
```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://your-production-api-url.com'
};
```

---

## 🔐 **Step 2: Authentication Setup**

### 2.1 Auth Service

**`src/app/core/services/auth.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  userId: number;
  email: string;
  fullName: string;
  role: string; // 'Admin' | 'InternalUser' | 'ExternalUser'
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/auth`;
  private readonly tokenKey = 'jwt_token';
  private readonly userKey = 'user_data';

  constructor(private http: HttpClient) {}

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, { email, password })
      .pipe(
        tap(res => {
          localStorage.setItem(this.tokenKey, res.token);
          localStorage.setItem(this.userKey, JSON.stringify({
            userId: res.userId,
            email: res.email,
            fullName: res.fullName,
            role: res.role
          }));
        })
      );
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getUser(): { userId: number; email: string; fullName: string; role: string } | null {
    const userStr = localStorage.getItem(this.userKey);
    return userStr ? JSON.parse(userStr) : null;
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  isAdmin(): boolean {
    const user = this.getUser();
    return user?.role === 'Admin';
  }

  isInternalUser(): boolean {
    const user = this.getUser();
    return user?.role === 'InternalUser';
  }

  isExternalUser(): boolean {
    const user = this.getUser();
    return user?.role === 'ExternalUser';
  }
}
```

### 2.2 Auth Interceptor

**`src/app/core/interceptors/auth.interceptor.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private auth: AuthService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.auth.getToken();
    if (token) {
      const cloned = req.clone({
        setHeaders: { Authorization: `Bearer ${token}` }
      });
      return next.handle(cloned);
    }
    return next.handle(req);
  }
}
```

### 2.3 Auth Guard

**`src/app/core/guards/auth.guard.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (this.auth.isLoggedIn()) {
      return true;
    }
    this.router.navigate(['/login']);
    return false;
  }
}
```

### 2.4 Role Guard

**`src/app/core/guards/role.guard.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const requiredRole = route.data['role'] as string;
    const user = this.auth.getUser();

    if (user && user.role === requiredRole) {
      return true;
    }

    this.router.navigate(['/unauthorized']);
    return false;
  }
}
```

---

## 📦 **Step 3: Services**

### 3.1 User Service

**`src/app/core/services/user.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserDto {
  id: number;
  email: string;
  fullName: string;
  role: number; // 1=Admin, 2=InternalUser, 3=ExternalUser
  isActive: boolean;
}

export interface CreateUserDto {
  email: string;
  fullName: string;
  password: string;
  role: number;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private baseUrl = `${environment.apiBaseUrl}/api/admin`;

  constructor(private http: HttpClient) {}

  getAllUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.baseUrl}/users`);
  }

  getInternalUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.baseUrl}/users/internal`);
  }

  createUser(dto: CreateUserDto): Observable<UserDto> {
    return this.http.post<UserDto>(`${this.baseUrl}/users`, dto);
  }
}
```

### 3.2 Workflow Service

**`src/app/core/services/workflow.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface WorkflowSummaryDto {
  id: number;
  title: string;
  status: number; // 0=Draft, 1=PendingInternalReview, 2=InternalApproved, 3=PendingExternalReview, 4=Completed, 5=Rejected
  internalReviewerName: string;
  externalReviewerEmail: string;
  pdfFilePath?: string;
  pdfFileName?: string;
}

export interface CreateWorkflowDto {
  title: string;
  internalReviewerId: number;
  externalReviewerEmail: string;
}

@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private baseUrl = `${environment.apiBaseUrl}/api/workflows`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  createWorkflow(dto: CreateWorkflowDto, file: File): Observable<WorkflowSummaryDto> {
    const user = this.auth.getUser();
    if (!user) throw new Error('User not logged in');

    const formData = new FormData();
    formData.append('Title', dto.title);
    formData.append('InternalReviewerId', dto.internalReviewerId.toString());
    formData.append('ExternalReviewerEmail', dto.externalReviewerEmail);
    formData.append('File', file);

    return this.http.post<WorkflowSummaryDto>(
      `${this.baseUrl}?currentUserId=${user.userId}`,
      formData
    );
  }

  getWorkflowById(id: number): Observable<WorkflowSummaryDto> {
    return this.http.get<WorkflowSummaryDto>(`${this.baseUrl}/${id}`);
  }
}
```

### 3.3 Internal Review Service

**`src/app/core/services/internal-review.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { WorkflowSummaryDto } from './workflow.service';

export interface StampDto {
  label: string;
  pageNumber: number;
  x: number;
  y: number;
}

export interface InternalReviewApprovalDto {
  workflowId: number;
  stamp: StampDto;
}

@Injectable({ providedIn: 'root' })
export class InternalReviewService {
  private baseUrl = `${environment.apiBaseUrl}/api/internal-review`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  getAssignedWorkflows(): Observable<WorkflowSummaryDto[]> {
    const user = this.auth.getUser();
    if (!user) throw new Error('User not logged in');

    return this.http.get<WorkflowSummaryDto[]>(
      `${this.baseUrl}/assigned?reviewerUserId=${user.userId}`
    );
  }

  approve(workflowId: number, stamp: StampDto): Observable<void> {
    const user = this.auth.getUser();
    if (!user) throw new Error('User not logged in');

    const dto: InternalReviewApprovalDto = { workflowId, stamp };
    return this.http.post<void>(
      `${this.baseUrl}/approve?reviewerUserId=${user.userId}`,
      dto
    );
  }
}
```

### 3.4 External Review Service

**`src/app/core/services/external-review.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { WorkflowSummaryDto } from './workflow.service';
import { StampDto } from './internal-review.service';

export interface ExternalOtpValidationDto {
  token: string;
  otp: string;
}

export interface ExternalApprovalDto {
  token: string;
  stamp: StampDto;
}

@Injectable({ providedIn: 'root' })
export class ExternalReviewService {
  private baseUrl = `${environment.apiBaseUrl}/api/external-review`;

  constructor(private http: HttpClient) {}

  getWorkflowByToken(token: string): Observable<WorkflowSummaryDto> {
    return this.http.get<WorkflowSummaryDto>(`${this.baseUrl}/workflow?token=${token}`);
  }

  validateOtp(token: string, otp: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.baseUrl}/validate-otp`, { token, otp });
  }

  approve(token: string, stamp: StampDto): Observable<void> {
    const dto: ExternalApprovalDto = { token, stamp };
    return this.http.post<void>(`${this.baseUrl}/approve`, dto);
  }
}
```

---

## 🎨 **Step 4: Components**

### 4.1 Login Component

**`src/app/auth/login/login.component.ts`:**
```typescript
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  loginForm: FormGroup;
  errorMessage: string = '';

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]]
    });
  }

  onSubmit(): void {
    if (this.loginForm.valid) {
      const { email, password } = this.loginForm.value;
      this.auth.login(email, password).subscribe({
        next: (response) => {
          // Navigate based on role
          if (response.role === 'Admin') {
            this.router.navigate(['/admin']);
          } else if (response.role === 'InternalUser') {
            this.router.navigate(['/internal']);
          } else {
            this.router.navigate(['/external']);
          }
        },
        error: (err) => {
          this.errorMessage = 'Invalid email or password';
          console.error('Login error:', err);
        }
      });
    }
  }
}
```

**`src/app/auth/login/login.component.html`:**
```html
<div class="login-container">
  <div class="login-card">
    <h2>Login</h2>
    <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
      <div class="form-group">
        <label>Email</label>
        <input type="email" formControlName="email" class="form-control" />
        <small *ngIf="loginForm.get('email')?.hasError('required')" class="text-danger">
          Email is required
        </small>
      </div>

      <div class="form-group">
        <label>Password</label>
        <input type="password" formControlName="password" class="form-control" />
        <small *ngIf="loginForm.get('password')?.hasError('required')" class="text-danger">
          Password is required
        </small>
      </div>

      <div *ngIf="errorMessage" class="alert alert-danger">
        {{ errorMessage }}
      </div>

      <button type="submit" [disabled]="loginForm.invalid" class="btn btn-primary">
        Login
      </button>
    </form>
  </div>
</div>
```

### 4.2 Create Workflow Component (Internal User)

**`src/app/internal/create-workflow/create-workflow.component.ts`:**
```typescript
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { WorkflowService, CreateWorkflowDto } from '../../core/services/workflow.service';
import { UserService, UserDto } from '../../core/services/user.service';

@Component({
  selector: 'app-create-workflow',
  templateUrl: './create-workflow.component.html',
  styleUrls: ['./create-workflow.component.scss']
})
export class CreateWorkflowComponent implements OnInit {
  workflowForm: FormGroup;
  internalUsers: UserDto[] = [];
  selectedFile: File | null = null;
  loading = false;

  constructor(
    private fb: FormBuilder,
    private workflowService: WorkflowService,
    private userService: UserService,
    private router: Router
  ) {
    this.workflowForm = this.fb.group({
      title: ['', [Validators.required]],
      internalReviewerId: [null, [Validators.required]],
      externalReviewerEmail: ['', [Validators.required, Validators.email]],
      file: [null, [Validators.required]]
    });
  }

  ngOnInit(): void {
    this.loadInternalUsers();
  }

  loadInternalUsers(): void {
    this.userService.getInternalUsers().subscribe({
      next: (users) => {
        this.internalUsers = users;
      },
      error: (err) => console.error('Error loading internal users:', err)
    });
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file && file.type === 'application/pdf') {
      this.selectedFile = file;
      this.workflowForm.patchValue({ file });
    } else {
      alert('Please select a PDF file');
    }
  }

  onSubmit(): void {
    if (this.workflowForm.valid && this.selectedFile) {
      this.loading = true;
      const dto: CreateWorkflowDto = {
        title: this.workflowForm.value.title,
        internalReviewerId: this.workflowForm.value.internalReviewerId,
        externalReviewerEmail: this.workflowForm.value.externalReviewerEmail
      };

      this.workflowService.createWorkflow(dto, this.selectedFile).subscribe({
        next: (workflow) => {
          this.loading = false;
          // Navigate to workflow execute screen
          this.router.navigate(['/internal/workflow', workflow.id]);
        },
        error: (err) => {
          this.loading = false;
          console.error('Error creating workflow:', err);
          alert('Failed to create workflow');
        }
      });
    }
  }
}
```

**`src/app/internal/create-workflow/create-workflow.component.html`:**
```html
<div class="create-workflow-container">
  <h2>Create New Workflow</h2>
  <form [formGroup]="workflowForm" (ngSubmit)="onSubmit()">
    <div class="form-group">
      <label>Title</label>
      <input type="text" formControlName="title" class="form-control" />
    </div>

    <div class="form-group">
      <label>Internal Reviewer</label>
      <select formControlName="internalReviewerId" class="form-control">
        <option value="">Select Internal Reviewer</option>
        <option *ngFor="let user of internalUsers" [value]="user.id">
          {{ user.fullName }} ({{ user.email }})
        </option>
      </select>
    </div>

    <div class="form-group">
      <label>External Reviewer Email</label>
      <input type="email" formControlName="externalReviewerEmail" class="form-control" />
    </div>

    <div class="form-group">
      <label>PDF File</label>
      <input type="file" accept=".pdf" (change)="onFileSelected($event)" class="form-control" />
      <small *ngIf="selectedFile">{{ selectedFile.name }}</small>
    </div>

    <button type="submit" [disabled]="workflowForm.invalid || loading" class="btn btn-primary">
      {{ loading ? 'Creating...' : 'Submit' }}
    </button>
  </form>
</div>
```

### 4.3 Internal Reviewer - Assigned Workflows List

**`src/app/internal/assigned-workflows/assigned-workflows.component.ts`:**
```typescript
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { InternalReviewService } from '../../core/services/internal-review.service';
import { WorkflowSummaryDto } from '../../core/services/workflow.service';

@Component({
  selector: 'app-assigned-workflows',
  templateUrl: './assigned-workflows.component.html',
  styleUrls: ['./assigned-workflows.component.scss']
})
export class AssignedWorkflowsComponent implements OnInit {
  workflows: WorkflowSummaryDto[] = [];
  loading = false;

  constructor(
    private internalReviewService: InternalReviewService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadWorkflows();
  }

  loadWorkflows(): void {
    this.loading = true;
    this.internalReviewService.getAssignedWorkflows().subscribe({
      next: (workflows) => {
        this.workflows = workflows;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading workflows:', err);
        this.loading = false;
      }
    });
  }

  openWorkflow(workflowId: number): void {
    this.router.navigate(['/internal/review', workflowId]);
  }
}
```

**`src/app/internal/assigned-workflows/assigned-workflows.component.html`:**
```html
<div class="assigned-workflows-container">
  <h2>Assigned Workflows</h2>
  <div *ngIf="loading">Loading...</div>
  <div *ngIf="!loading && workflows.length === 0" class="alert alert-info">
    No workflows assigned to you.
  </div>
  <table *ngIf="!loading && workflows.length > 0" class="table">
    <thead>
      <tr>
        <th>Title</th>
        <th>Status</th>
        <th>External Reviewer</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let workflow of workflows">
        <td>{{ workflow.title }}</td>
        <td>{{ getStatusText(workflow.status) }}</td>
        <td>{{ workflow.externalReviewerEmail }}</td>
        <td>
          <button (click)="openWorkflow(workflow.id)" class="btn btn-primary">
            Review
          </button>
        </td>
      </tr>
    </tbody>
  </table>
</div>
```

### 4.4 Internal Reviewer - Review Workflow (with PDF Viewer)

**`src/app/internal/review-workflow/review-workflow.component.ts`:**
```typescript
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { WorkflowService, WorkflowSummaryDto } from '../../core/services/workflow.service';
import { InternalReviewService, StampDto } from '../../core/services/internal-review.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-review-workflow',
  templateUrl: './review-workflow.component.html',
  styleUrls: ['./review-workflow.component.scss']
})
export class ReviewWorkflowComponent implements OnInit {
  workflowId!: number;
  workflow: WorkflowSummaryDto | null = null;
  serviceUrl = `${environment.apiBaseUrl}/PdfViewer`;
  documentPath: string = '';
  stampForm = {
    label: 'INTERNAL APPROVED',
    pageNumber: 1,
    x: 100,
    y: 150
  };
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private workflowService: WorkflowService,
    private internalReviewService: InternalReviewService
  ) {}

  ngOnInit(): void {
    this.workflowId = +this.route.snapshot.paramMap.get('id')!;
    this.loadWorkflow();
  }

  loadWorkflow(): void {
    this.workflowService.getWorkflowById(this.workflowId).subscribe({
      next: (workflow) => {
        this.workflow = workflow;
        // Use filename for Syncfusion viewer
        this.documentPath = workflow.pdfFileName || '';
      },
      error: (err) => {
        console.error('Error loading workflow:', err);
        alert('Failed to load workflow');
      }
    });
  }

  onStampPositionChange(event: any): void {
    // Handle click on PDF to set stamp position
    // This depends on Syncfusion PDF Viewer API
    // You'll need to implement based on Syncfusion's click event
  }

  approve(): void {
    if (!this.workflow) return;

    this.loading = true;
    const stamp: StampDto = {
      label: this.stampForm.label,
      pageNumber: this.stampForm.pageNumber,
      x: this.stampForm.x,
      y: this.stampForm.y
    };

    this.internalReviewService.approve(this.workflowId, stamp).subscribe({
      next: () => {
        this.loading = false;
        alert('Workflow approved! Email sent to external reviewer.');
        this.router.navigate(['/internal']);
      },
      error: (err) => {
        this.loading = false;
        console.error('Error approving workflow:', err);
        alert('Failed to approve workflow');
      }
    });
  }
}
```

**`src/app/internal/review-workflow/review-workflow.component.html`:**
```html
<div class="review-workflow-container">
  <h2>Review Workflow: {{ workflow?.title }}</h2>
  
  <div *ngIf="workflow">
    <div class="pdf-viewer-container">
      <ejs-pdfviewer
        id="pdfViewer"
        [serviceUrl]="serviceUrl"
        [documentPath]="documentPath"
        style="height: 600px; display: block;">
      </ejs-pdfviewer>
    </div>

    <div class="stamp-form">
      <h3>Apply Internal Stamp</h3>
      <div class="form-group">
        <label>Label</label>
        <input [(ngModel)]="stampForm.label" class="form-control" />
      </div>
      <div class="form-group">
        <label>Page Number</label>
        <input type="number" [(ngModel)]="stampForm.pageNumber" class="form-control" />
      </div>
      <div class="form-group">
        <label>X Position</label>
        <input type="number" [(ngModel)]="stampForm.x" class="form-control" />
      </div>
      <div class="form-group">
        <label>Y Position</label>
        <input type="number" [(ngModel)]="stampForm.y" class="form-control" />
      </div>
      <button (click)="approve()" [disabled]="loading" class="btn btn-success">
        {{ loading ? 'Approving...' : 'Approve & Send to External Reviewer' }}
      </button>
    </div>
  </div>
</div>
```

### 4.5 External Reviewer - OTP Entry & Review

**`src/app/external/external-review/external-review.component.ts`:**
```typescript
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ExternalReviewService } from '../../core/services/external-review.service';
import { WorkflowService, WorkflowSummaryDto } from '../../core/services/workflow.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-external-review',
  templateUrl: './external-review.component.html',
  styleUrls: ['./external-review.component.scss']
})
export class ExternalReviewComponent implements OnInit {
  token: string = '';
  workflow: WorkflowSummaryDto | null = null;
  otp: string = '';
  otpValidated = false;
  serviceUrl = `${environment.apiBaseUrl}/PdfViewer`;
  documentPath: string = '';
  stampForm = {
    label: 'EXTERNAL APPROVED',
    pageNumber: 1,
    x: 300,
    y: 150
  };
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private externalReviewService: ExternalReviewService
  ) {}

  ngOnInit(): void {
    // Get token from URL query parameter
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    if (this.token) {
      this.loadWorkflow();
    }
  }

  loadWorkflow(): void {
    this.externalReviewService.getWorkflowByToken(this.token).subscribe({
      next: (workflow) => {
        this.workflow = workflow;
        this.documentPath = workflow.pdfFileName || '';
      },
      error: (err) => {
        console.error('Error loading workflow:', err);
        alert('Invalid or expired token');
      }
    });
  }

  validateOtp(): void {
    if (!this.otp || this.otp.length !== 6) {
      alert('Please enter a valid 6-digit OTP');
      return;
    }

    this.loading = true;
    this.externalReviewService.validateOtp(this.token, this.otp).subscribe({
      next: (isValid) => {
        this.loading = false;
        if (isValid) {
          this.otpValidated = true;
        } else {
          alert('Invalid OTP. Please try again.');
        }
      },
      error: (err) => {
        this.loading = false;
        console.error('Error validating OTP:', err);
        alert('Failed to validate OTP');
      }
    });
  }

  approve(): void {
    if (!this.workflow) return;

    this.loading = true;
    const stamp = {
      label: this.stampForm.label,
      pageNumber: this.stampForm.pageNumber,
      x: this.stampForm.x,
      y: this.stampForm.y
    };

    this.externalReviewService.approve(this.token, stamp).subscribe({
      next: () => {
        this.loading = false;
        alert('Document approved! Workflow completed.');
        // Redirect to success page or show completion message
      },
      error: (err) => {
        this.loading = false;
        console.error('Error approving workflow:', err);
        alert('Failed to approve workflow');
      }
    });
  }
}
```

**`src/app/external/external-review/external-review.component.html`:**
```html
<div class="external-review-container">
  <h2>External Review</h2>

  <div *ngIf="!workflow" class="alert alert-danger">
    Invalid or expired token.
  </div>

  <div *ngIf="workflow">
    <div *ngIf="!otpValidated" class="otp-section">
      <h3>Enter OTP</h3>
      <p>Please enter the OTP you received in your email.</p>
      <div class="form-group">
        <input 
          type="text" 
          [(ngModel)]="otp" 
          maxlength="6" 
          placeholder="000000"
          class="form-control" />
      </div>
      <button (click)="validateOtp()" [disabled]="loading || otp.length !== 6" class="btn btn-primary">
        {{ loading ? 'Validating...' : 'Validate OTP' }}
      </button>
    </div>

    <div *ngIf="otpValidated">
      <h3>Review Document: {{ workflow.title }}</h3>
      
      <div class="pdf-viewer-container">
        <ejs-pdfviewer
          id="pdfViewer"
          [serviceUrl]="serviceUrl"
          [documentPath]="documentPath"
          style="height: 600px; display: block;">
        </ejs-pdfviewer>
      </div>

      <div class="stamp-form">
        <h3>Apply External Stamp</h3>
        <div class="form-group">
          <label>Label</label>
          <input [(ngModel)]="stampForm.label" class="form-control" />
        </div>
        <div class="form-group">
          <label>Page Number</label>
          <input type="number" [(ngModel)]="stampForm.pageNumber" class="form-control" />
        </div>
        <div class="form-group">
          <label>X Position</label>
          <input type="number" [(ngModel)]="stampForm.x" class="form-control" />
        </div>
        <div class="form-group">
          <label>Y Position</label>
          <input type="number" [(ngModel)]="stampForm.y" class="form-control" />
        </div>
        <button (click)="approve()" [disabled]="loading" class="btn btn-success">
          {{ loading ? 'Approving...' : 'Approve & Complete Workflow' }}
        </button>
      </div>
    </div>
  </div>
</div>
```

---

## 🛣️ **Step 5: Routing**

**`src/app/app-routing.module.ts`:**
```typescript
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { CreateWorkflowComponent } from './internal/create-workflow/create-workflow.component';
import { AssignedWorkflowsComponent } from './internal/assigned-workflows/assigned-workflows.component';
import { ReviewWorkflowComponent } from './internal/review-workflow/review-workflow.component';
import { ExternalReviewComponent } from './external/external-review/external-review.component';
import { AuthGuard } from './core/guards/auth.guard';
import { RoleGuard } from './core/guards/role.guard';

const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  
  // Internal User Routes
  {
    path: 'internal',
    canActivate: [AuthGuard, RoleGuard],
    data: { role: 'InternalUser' },
    children: [
      { path: 'create', component: CreateWorkflowComponent },
      { path: 'assigned', component: AssignedWorkflowsComponent },
      { path: 'review/:id', component: ReviewWorkflowComponent },
      { path: 'workflow/:id', component: ReviewWorkflowComponent } // Workflow execute screen
    ]
  },
  
  // External User Route (no auth required, uses token)
  { path: 'external-review', component: ExternalReviewComponent },
  
  // Admin Routes (add as needed)
  // { path: 'admin', component: AdminComponent, canActivate: [AuthGuard, RoleGuard], data: { role: 'Admin' } },
  
  { path: '**', redirectTo: '/login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
```

---

## 📦 **Step 6: App Module Configuration**

**`src/app/app.module.ts`:**
```typescript
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { PdfViewerModule } from '@syncfusion/ej2-angular-pdfviewer';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LoginComponent } from './auth/login/login.component';
import { CreateWorkflowComponent } from './internal/create-workflow/create-workflow.component';
import { AssignedWorkflowsComponent } from './internal/assigned-workflows/assigned-workflows.component';
import { ReviewWorkflowComponent } from './internal/review-workflow/review-workflow.component';
import { ExternalReviewComponent } from './external/external-review/external-review.component';

import { AuthInterceptor } from './core/interceptors/auth.interceptor';

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    CreateWorkflowComponent,
    AssignedWorkflowsComponent,
    ReviewWorkflowComponent,
    ExternalReviewComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HttpClientModule,
    FormsModule,
    ReactiveFormsModule,
    PdfViewerModule
  ],
  providers: [
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
```

---

## 🎨 **Step 7: Styling (Basic)**

**`src/styles.scss`:**
```scss
@import '~@syncfusion/ej2-base/styles/material.css';
@import '~@syncfusion/ej2-buttons/styles/material.css';
@import '~@syncfusion/ej2-popups/styles/material.css';
@import '~@syncfusion/ej2-splitbuttons/styles/material.css';
@import '~@syncfusion/ej2-inputs/styles/material.css';
@import '~@syncfusion/ej2-lists/styles/material.css';
@import '~@syncfusion/ej2-navigations/styles/material.css';
@import '~@syncfusion/ej2-dropdowns/styles/material.css';
@import '~@syncfusion/ej2-pdfviewer/styles/material.css';

// Basic styling
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
}

.login-card {
  width: 400px;
  padding: 2rem;
  border: 1px solid #ddd;
  border-radius: 8px;
}

.form-group {
  margin-bottom: 1rem;
}

.form-control {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.btn-primary {
  background-color: #007bff;
  color: white;
}

.btn-success {
  background-color: #28a745;
  color: white;
}

.table {
  width: 100%;
  border-collapse: collapse;
}

.table th, .table td {
  padding: 0.75rem;
  border: 1px solid #ddd;
}
```

---

## ✅ **Summary**

This guide provides:
- ✅ Complete service layer (Auth, User, Workflow, Internal Review, External Review)
- ✅ All components needed for the workflow
- ✅ Syncfusion PDF Viewer integration
- ✅ Authentication with JWT
- ✅ Routing with guards
- ✅ Form handling
- ✅ API integration

**Next Steps:**
1. Copy the code into your Angular project
2. Adjust styling as needed
3. Test each workflow step
4. Add error handling and loading states
5. Add navigation menu/header component
6. Test with your backend API

**Important Notes:**
- Syncfusion PDF Viewer requires a license key (set in `app.module.ts` or `main.ts`)
- The PDF viewer expects the file name, not full path (backend handles path resolution)
- Make sure CORS is enabled on your backend (already done)
- Test with the seeded users: `admin@company.com` / `Admin123!`

---

## 🔗 **API Endpoints Reference**

All endpoints are documented in `REQUIREMENTS_COVERAGE.md`. The Angular services above call:
- `POST /api/auth/login`
- `GET /api/admin/users/internal`
- `POST /api/workflows`
- `GET /api/workflows/{id}`
- `GET /api/internal-review/assigned`
- `POST /api/internal-review/approve`
- `GET /api/external-review/workflow?token={token}`
- `POST /api/external-review/validate-otp`
- `POST /api/external-review/approve`
- `POST /PdfViewer/*` (Syncfusion endpoints)

---

**Ready to build!** 🚀

