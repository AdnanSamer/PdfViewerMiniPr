# Syncfusion PDF Viewer Configuration Guide

## Fix: "Client-side error is found. Please check the custom headers..."

This error occurs when the Syncfusion PDF Viewer cannot properly communicate with the backend API. The solution is to configure `ajaxRequestSettings` to send the JWT token in the request headers.

---

## Solution 1: Configure ajaxRequestSettings in Angular Component

### Step 1: Update Your Angular Component

In your Angular component where you use the PDF Viewer (e.g., `internal-review.component.ts`), add the following configuration:

```typescript
import { Component, OnInit, ViewChild } from '@angular/core';
import { PdfViewerComponent } from '@syncfusion/ej2-angular-pdfviewer';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-internal-review',
  templateUrl: './internal-review.component.html'
})
export class InternalReviewComponent implements OnInit {
  @ViewChild('pdfviewer') pdfviewer!: PdfViewerComponent;
  
  serviceUrl = 'http://localhost:7121/PdfViewer';
  
  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    // Configure PDF Viewer after view init
    setTimeout(() => {
      this.configurePdfViewer();
    }, 100);
  }

  ngAfterViewInit(): void {
    this.configurePdfViewer();
  }

  private configurePdfViewer(): void {
    if (!this.pdfviewer) return;

    const token = this.authService.getToken();
    if (!token) {
      console.error('No authentication token found');
      return;
    }

    // Configure ajaxRequestSettings to include JWT token
    this.pdfviewer.ajaxRequestSettings = {
      ajaxHeaders: [
        {
          headerName: 'Authorization',
          headerValue: `Bearer ${token}`
        },
        {
          headerName: 'Content-Type',
          headerValue: 'application/json'
        }
      ],
      withCredentials: false
    };

    // Configure server action settings (optional, but recommended)
    this.pdfviewer.serverActionSettings = {
      load: 'Load',
      fileUpload: 'FileUpload',
      download: 'Download',
      print: 'PrintImages',
      renderPages: 'RenderPdfPages',
      renderThumbnail: 'RenderThumbnailImages',
      renderComments: 'RenderAnnotationComments',
      bookmark: 'Bookmarks',
      exportAnnotations: 'ExportAnnotations',
      importAnnotations: 'ImportAnnotations',
      renderText: 'RenderPdfTexts',
      unload: 'Unload'
    };

    console.log('PDF Viewer configured with authentication headers');
  }

  // Reload configuration when token changes
  onTokenRefresh(): void {
    this.configurePdfViewer();
  }
}
```

### Step 2: Update Your HTML Template

```html
<ejs-pdfviewer
  #pdfviewer
  id="pdfViewer"
  [serviceUrl]="serviceUrl"
  [documentPath]="documentPath"
  [enableToolbar]="true"
  [enableNavigationToolbar]="true"
  [enableAnnotationToolbar]="true"
  (documentLoad)="onDocumentLoad($event)">
</ejs-pdfviewer>
```

---

## Solution 2: Alternative - Using HTTP Interceptor (Recommended)

This approach automatically adds the token to all requests, including PDF Viewer requests.

### Step 1: Create HTTP Interceptor

**`src/app/core/interceptors/auth.interceptor.ts`:**

```typescript
import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private authService: AuthService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.authService.getToken();
    
    if (token) {
      // Clone the request and add the authorization header
      const clonedRequest = request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
      return next.handle(clonedRequest);
    }
    
    return next.handle(request);
  }
}
```

### Step 2: Register the Interceptor

**`src/app/app.config.ts` (or `app.module.ts` for older Angular versions):**

```typescript
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(
      withInterceptors([AuthInterceptor])
    ),
    // ... other providers
  ]
};
```

**For Angular < 15 (app.module.ts):**

```typescript
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';

@NgModule({
  // ...
  providers: [
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    }
  ]
})
export class AppModule { }
```

---

## Solution 3: Allow Anonymous Access to PdfViewerController (If PDFs Should Be Public)

If PDF viewing should not require authentication, you can allow anonymous access:

**`PdfViewerMiniPr/Controllers/PdfViewerController.cs`:**

```csharp
using Microsoft.AspNetCore.Authorization;

[Route("[controller]")]
[ApiController]
[AllowAnonymous] // Add this attribute
public class PdfViewerController : ControllerBase
{
    // ... rest of the controller
}
```

**Note:** Only use this if PDFs should be accessible without authentication. For your workflow system, you likely want authentication.

---

## Solution 4: Configure CORS Properly (If Still Having Issues)

Ensure your backend CORS configuration allows the Authorization header:

**`Program.cs`:**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()  // This should allow Authorization header
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

---

## Recommended Approach

**Use Solution 2 (HTTP Interceptor)** because:
1. It automatically handles all HTTP requests
2. No need to configure each component separately
3. Centralized authentication logic
4. Works with Syncfusion PDF Viewer automatically

Then also configure `ajaxRequestSettings` in your PDF Viewer component (Solution 1) as a backup to ensure the headers are sent correctly.

---

## Testing

After implementing the solution:

1. Open browser DevTools (F12)
2. Go to Network tab
3. Load a PDF in the viewer
4. Check the request to `/PdfViewer/Load`
5. Verify the `Authorization` header is present: `Bearer <your-token>`

If the header is present but you still get errors, check:
- Token is valid and not expired
- Backend is accepting the token (check server logs)
- CORS is properly configured

---

## Additional Notes

- The `ajaxRequestSettings` must be set **before** the PDF Viewer makes any requests
- If using `ngAfterViewInit`, ensure the viewer is fully initialized
- Consider refreshing the token if it expires during a session
- The `withCredentials: false` is usually fine unless you need cookies

