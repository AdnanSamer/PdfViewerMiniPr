# Quick Fix for Syncfusion PDF Viewer Error

## Immediate Solution

Add this code to your Angular component that uses the PDF Viewer:

### In your Component TypeScript file:

```typescript
import { Component, ViewChild, AfterViewInit } from '@angular/core';
import { PdfViewerComponent } from '@syncfusion/ej2-angular-pdfviewer';
import { AuthService } from '../core/services/auth.service'; // Adjust path as needed

export class YourComponent implements AfterViewInit {
  @ViewChild('pdfviewer') pdfviewer!: PdfViewerComponent;
  
  serviceUrl = 'http://localhost:7121/PdfViewer';
  
  constructor(private authService: AuthService) {}

  ngAfterViewInit(): void {
    // Configure PDF Viewer with authentication
    setTimeout(() => {
      this.setupPdfViewerAuth();
    }, 100);
  }

  private setupPdfViewerAuth(): void {
    if (!this.pdfviewer) {
      console.warn('PDF Viewer not initialized');
      return;
    }

    const token = this.authService.getToken();
    if (!token) {
      console.error('No authentication token available');
      return;
    }

    // Set ajaxRequestSettings to include JWT token
    this.pdfviewer.ajaxRequestSettings = {
      ajaxHeaders: [
        {
          headerName: 'Authorization',
          headerValue: `Bearer ${token}`
        }
      ],
      withCredentials: false
    };

    console.log('PDF Viewer authentication configured');
  }
}
```

### In your Component HTML template:

Make sure you have a ViewChild reference:

```html
<ejs-pdfviewer
  #pdfviewer
  id="pdfViewer"
  [serviceUrl]="serviceUrl"
  [documentPath]="documentPath"
  [enableToolbar]="true">
</ejs-pdfviewer>
```

---

## Alternative: Allow Anonymous Access (If PDFs Should Be Public)

If PDF viewing should not require authentication, add `[AllowAnonymous]` to the controller:

**File: `PdfViewerMiniPr/Controllers/PdfViewerController.cs`**

Add at the top of the class:

```csharp
using Microsoft.AspNetCore.Authorization;

[Route("[controller]")]
[ApiController]
[AllowAnonymous]  // Add this line
public class PdfViewerController : ControllerBase
{
    // ... existing code
}
```

**Note:** Only use this if PDFs should be accessible without authentication. For your workflow system, you likely want to keep authentication and use the first solution.

---

## Verify the Fix

1. Open browser DevTools (F12)
2. Go to Network tab
3. Load a PDF
4. Find the request to `/PdfViewer/Load`
5. Check Headers section
6. Verify `Authorization: Bearer <token>` is present

If the header is present, the error should be resolved!

