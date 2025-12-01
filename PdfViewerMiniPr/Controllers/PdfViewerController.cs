using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Infrastructure.Database;
using PdfViewrMiniPr.Infrastructure.Repositories;
using Syncfusion.EJ2.PdfViewer;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PdfViewerMiniPr.Controllers;

[Route("[controller]")]
[ApiController]
public class PdfViewerController : ControllerBase
{
    private IWebHostEnvironment _hostingEnvironment;
    //Initialize the memory cache object   
    public IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly IWorkflowRepository _workflowRepository;

    public PdfViewerController(IWebHostEnvironment hostingEnvironment, IMemoryCache cache, AppDbContext db, IWorkflowRepository workflowRepository)
    {
        _hostingEnvironment = hostingEnvironment;
        _cache = cache;
        _db = db;
        _workflowRepository = workflowRepository;
        Console.WriteLine("PdfViewerController initialized");
    }

    // Helper method to safely convert Dictionary<string, object> to Dictionary<string, string>
    // Handles booleans, numbers, and null values
    private Dictionary<string, string> ConvertToStringDictionary(Dictionary<string, object>? jsonObject)
    {
        var result = new Dictionary<string, string>();
        if (jsonObject == null) return result;

        foreach (var kvp in jsonObject)
        {
            string value;
            if (kvp.Value == null)
            {
                value = string.Empty;
            }
            else if (kvp.Value is bool boolValue)
            {
                value = boolValue.ToString().ToLowerInvariant(); // "true" or "false"
            }
            else if (kvp.Value is JToken jToken)
            {
                value = jToken.ToString();
            }
            else
            {
                value = kvp.Value.ToString() ?? string.Empty;
            }
            result[kvp.Key] = value;
        }
        return result;
    }

    // Helper method to safely get boolean value from Dictionary<string, object>
    private bool GetBoolValue(Dictionary<string, object>? jsonObject, string key, bool defaultValue = false)
    {
        if (jsonObject == null || !jsonObject.TryGetValue(key, out var value))
            return defaultValue;

        if (value is bool boolValue)
            return boolValue;

        if (value is string strValue)
            return bool.TryParse(strValue, out var parsed) && parsed;

        return defaultValue;
    }

    // Helper method to safely get string value from Dictionary<string, object>
    private string? GetStringValue(Dictionary<string, object>? jsonObject, string key)
    {
        if (jsonObject == null || !jsonObject.TryGetValue(key, out var value))
            return null;

        return value?.ToString();
    }

    // Helper method to check if PDF is read-only for external users (status = Completed)
    private async Task<bool> IsPdfReadOnlyAsync(string? documentPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentPath))
            return false;

        try
        {
            // Get the full document path
            string fullPath = GetDocumentPath(documentPath);
            if (string.IsNullOrEmpty(fullPath))
                return false;

            // Find workflow by PDF file path
            var workflow = await _workflowRepository.GetByPdfFilePathAsync(fullPath, cancellationToken);
            
            // If workflow exists and status is Completed (4), it's read-only for external users
            if (workflow != null && workflow.Status == WorkflowStatus.Completed)
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if PDF is read-only: {ex.Message}");
            // On error, allow the operation (fail open for safety)
            return false;
        }
    }

    [HttpPost("Load")]
    //Post action for Loading the PDF documents   
    public async Task<IActionResult> Load([FromBody] Dictionary<string, object> jsonObject)
    {
        Console.WriteLine("Load called");
        try
        {
            //Initialize the PDF viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            MemoryStream stream = new MemoryStream();
            object jsonResult = new object();

            var document = GetStringValue(jsonObject, "document");
            Console.WriteLine($"Load - document preview: {(document?.Length > 100 ? document.Substring(0, 100) + "..." : document)}");
            
            if (!string.IsNullOrEmpty(document))
            {
                bool isFileName = GetBoolValue(jsonObject, "isFileName");
                Console.WriteLine($"Load - isFileName: {isFileName}");

                // Helper to check if a string looks like a filename (short, no base64 chars, has extension, etc.)
                bool LooksLikeFilename(string doc)
                {
                    if (string.IsNullOrEmpty(doc)) return false;
                    // If it's very short (like "17" or a filename), it's likely a filename
                    if (doc.Length < 50 && !doc.Contains("data:") && !doc.Contains("base64"))
                    {
                        // Check if it has a file extension or is just a number/name
                        if (doc.Contains(".") || doc.Length < 20)
                            return true;
                    }
                    // If it contains path separators, it's a filename
                    if (doc.Contains("/") || doc.Contains("\\"))
                        return true;
                    return false;
                }

                // If isFileName is false but it looks like a filename, treat it as a filename
                if (!isFileName && LooksLikeFilename(document))
                {
                    Console.WriteLine($"Load - Document looks like filename, treating as filename: {document}");
                    isFileName = true;
                }

                if (isFileName)
                {
                    string documentPath = GetDocumentPath(document);
                    if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                    {
                        Console.WriteLine($"Load - Loading from file: {documentPath}");
                        byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                        stream = new MemoryStream(bytes);
                    }
                    else
                    {
                        string fileName = document.Split(new string[] { "://" }, StringSplitOptions.None)[0];

                        if (fileName == "http" || fileName == "https")
                        {
                            Console.WriteLine($"Load - Loading from URL: {document}");
                            using var httpClient = new HttpClient();
                            byte[] pdfDoc = await httpClient.GetByteArrayAsync(document);
                            stream = new MemoryStream(pdfDoc);
                        }
                        else
                        {
                            Console.WriteLine($"Load - File not found: {document}");
                            return this.Content(document + " is not found");
                        }
                    }
                }
                else
                {
                    // Handle base64 string - could be a data URL or raw base64
                    string base64String = document;
                    
                    // Check if it's a data URL (e.g., "data:application/pdf;base64,<base64string>")
                    if (base64String.Contains(","))
                    {
                        // Extract the base64 part after the comma
                        int commaIndex = base64String.IndexOf(",");
                        base64String = base64String.Substring(commaIndex + 1);
                    }
                    
                    // Validate base64 string before conversion
                    base64String = base64String.Trim();
                    
                    // Remove any whitespace
                    base64String = base64String.Replace(" ", "").Replace("\n", "").Replace("\r", "");
                    
                    // Validate it's a valid base64 string format
                    if (string.IsNullOrEmpty(base64String))
                    {
                        return BadRequest(new { error = "Empty base64 string provided." });
                    }
                    
                    // Check if it looks like base64 (length should be multiple of 4, contains base64 chars)
                    bool isValidBase64Format = base64String.Length % 4 == 0 && 
                                               base64String.Length > 10 && // Base64 PDFs are usually long
                                               System.Text.RegularExpressions.Regex.IsMatch(base64String, @"^[A-Za-z0-9+/=]+$");
                    
                    if (!isValidBase64Format)
                    {
                        // If invalid base64 format, try treating it as a filename instead
                        Console.WriteLine($"Load - Invalid base64 format (length: {base64String.Length}), trying as filename: {document.Substring(0, Math.Min(50, document.Length))}...");
                        string documentPath = GetDocumentPath(document);
                        if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                        {
                            byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                            stream = new MemoryStream(bytes);
                        }
                        else
                        {
                            return BadRequest(new { error = $"Invalid document format. Expected base64 string or valid file path. Document preview: {document.Substring(0, Math.Min(100, document.Length))}" });
                        }
                    }
                    else
                    {
                        try
                        {
                            Console.WriteLine($"Load - Attempting base64 conversion (length: {base64String.Length})");
                            byte[] bytes = Convert.FromBase64String(base64String);
                            stream = new MemoryStream(bytes);
                            Console.WriteLine($"Load - Base64 conversion successful, PDF size: {bytes.Length} bytes");
                        }
                        catch (FormatException ex)
                        {
                            // If base64 conversion fails, try as filename
                            Console.WriteLine($"Load - Base64 conversion failed: {ex.Message}, trying as filename");
                            string documentPath = GetDocumentPath(document);
                            if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                            {
                                byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                                stream = new MemoryStream(bytes);
                            }
                            else
                            {
                                return BadRequest(new { error = $"Invalid base64 string format: {ex.Message}" });
                            }
                        }
                    }
                }
            }

            // Convert to string dictionary for Syncfusion
            var stringDict = ConvertToStringDictionary(jsonObject);
            jsonResult = pdfviewer.Load(stream, stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Load: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ValidatePassword")]
    public async Task<IActionResult> ValidatePassword([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            MemoryStream stream = new MemoryStream();

            var document = GetStringValue(jsonObject, "document");
            if (!string.IsNullOrEmpty(document))
            {
                bool isFileName = GetBoolValue(jsonObject, "isFileName");

                if (isFileName)
                {
                    string documentPath = GetDocumentPath(document);
                    if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                    {
                        byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                        stream = new MemoryStream(bytes);
                    }
                    else
                    {
                        string fileName = document.Split(new string[] { "://" }, StringSplitOptions.None)[0];

                        if (fileName == "http" || fileName == "https")
                        {
                            using var httpClient = new HttpClient();
                            byte[] pdfDoc = await httpClient.GetByteArrayAsync(document);
                            stream = new MemoryStream(pdfDoc);
                        }
                        else
                        {
                            return this.Content(document + " is not found");
                        }
                    }
                }
                else
                {
                    // Handle base64 string - could be a data URL or raw base64
                    string base64String = document;
                    
                    // Check if it's a data URL
                    if (base64String.Contains(","))
                    {
                        base64String = base64String.Substring(base64String.IndexOf(",") + 1);
                    }
                    
                    base64String = base64String.Trim();
                    base64String = base64String.Replace(" ", "").Replace("\n", "").Replace("\r", "");
                    
                    if (string.IsNullOrEmpty(base64String) || base64String.Length % 4 != 0)
                    {
                        return BadRequest(new { error = "Invalid base64 string format." });
                    }
                    
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(base64String);
                        stream = new MemoryStream(bytes);
                    }
                    catch (FormatException ex)
                    {
                        return BadRequest(new { error = $"Invalid base64 string: {ex.Message}" });
                    }
                }
            }

            string? password = GetStringValue(jsonObject, "password");
            var result = pdfviewer.Load(stream, password);

            return Content(JsonConvert.SerializeObject(result));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ValidatePassword: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Bookmarks")]
    //Post action for processing the bookmarks from the PDF documents
    public async Task<IActionResult> Bookmarks([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            var jsonResult = pdfviewer.GetBookmarks(stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Bookmarks: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("RenderPdfPages")]
    //Post action for processing the PDF documents  
    public async Task<IActionResult> RenderPdfPages([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            object jsonResult = pdfviewer.GetPage(stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderPdfPages: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("RenderPdfTexts")]
    //Post action for processing the PDF texts  
    public async Task<IActionResult> RenderPdfTexts([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            object jsonResult = pdfviewer.GetDocumentText(stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderPdfTexts: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("RenderThumbnailImages")]
    //Post action for rendering the ThumbnailImages
    public async Task<IActionResult> RenderThumbnailImages([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            object result = pdfviewer.GetThumbnailImages(stringDict);
            return Content(JsonConvert.SerializeObject(result));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderThumbnailImages: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("RenderAnnotationComments")]
    //Post action for rendering the annotations
    public async Task<IActionResult> RenderAnnotationComments([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            object jsonResult = pdfviewer.GetAnnotationComments(stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RenderAnnotationComments: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ExportAnnotations")]
    //Post action to export annotations
    public async Task<IActionResult> ExportAnnotations([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            // Check if PDF is read-only (status = Completed)
            var document = GetStringValue(jsonObject, "document") ?? GetStringValue(jsonObject, "fileName");
            if (await IsPdfReadOnlyAsync(document))
            {
                return StatusCode(403, new { error = "This PDF has been approved and is read-only." });
            }

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);

            // Check if JSON format is requested
            bool requestJsonFormat = stringDict.ContainsKey("annotationDataFormat") &&
                                     stringDict["annotationDataFormat"].Equals("Json", StringComparison.OrdinalIgnoreCase);

            string jsonResult = pdfviewer.ExportAnnotation(stringDict);

            // If JSON format was requested but we got a PDF data URL, try to extract JSON from it
            if (requestJsonFormat && !string.IsNullOrWhiteSpace(jsonResult) && jsonResult.StartsWith("data:application/pdf"))
            {
                // The Syncfusion ExportAnnotation might return PDF when annotations are empty
                // Return empty JSON array instead
                jsonResult = "[]";
            }

            // Optionally persist annotations to DB when documentId provided
            var documentIdStr = GetStringValue(jsonObject, "documentId");
            if (!string.IsNullOrEmpty(documentIdStr) && int.TryParse(documentIdStr, out var docId))
            {
                var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null)
                {
                    doc.AnnotationsJson = jsonResult;
                    doc.UpdatedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

            return Content(jsonResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExportAnnotations: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ImportAnnotations")]
    //Post action to import annotations
    public async Task<IActionResult> ImportAnnotations([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            // Check if PDF is read-only (status = Completed)
            var document = GetStringValue(jsonObject, "document") ?? GetStringValue(jsonObject, "fileName");
            if (await IsPdfReadOnlyAsync(document))
            {
                return StatusCode(403, new { error = "This PDF has been approved and is read-only." });
            }

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            string jsonResult = string.Empty;
            object? JsonResult = null;

            // If documentId provided and we have persisted annotations, return them
            var documentIdStr = GetStringValue(jsonObject, "documentId");
            if (!string.IsNullOrEmpty(documentIdStr) && int.TryParse(documentIdStr, out var docId))
            {
                var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.AnnotationsJson))
                {
                    return Content(doc.AnnotationsJson);
                }
            }

            var fileName = GetStringValue(jsonObject, "fileName");
            if (!string.IsNullOrEmpty(fileName))
            {
                string documentPath = GetDocumentPath(fileName);
                if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                {
                    jsonResult = await System.IO.File.ReadAllTextAsync(documentPath);
                    string[] searchStrings = { "textMarkupAnnotation", "measureShapeAnnotation", "freeTextAnnotation", "stampAnnotations", "signatureInkAnnotation", "stickyNotesAnnotation", "signatureAnnotation", "AnnotationType" };
                    bool isnewJsonFile = !searchStrings.Any(jsonResult.Contains);
                    if (isnewJsonFile)
                    {
                        byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                        stringDict["importedData"] = Convert.ToBase64String(bytes);
                        JsonResult = pdfviewer.ImportAnnotation(stringDict);
                        jsonResult = JsonConvert.SerializeObject(JsonResult);
                    }
                }
                else
                {
                    var docValue = GetStringValue(jsonObject, "document") ?? fileName;
                    return this.Content(docValue + " is not found");
                }
            }
            else
            {
                var importedData = GetStringValue(jsonObject, "importedData");
                if (!string.IsNullOrEmpty(importedData))
                {
                    string extension = Path.GetExtension(importedData);
                    if (extension != ".xfdf")
                    {
                        JsonResult = pdfviewer.ImportAnnotation(stringDict);
                        return Content(JsonConvert.SerializeObject(JsonResult));
                    }
                    else
                    {
                        string documentPath = GetDocumentPath(importedData);
                        if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                        {
                            byte[] bytes = await System.IO.File.ReadAllBytesAsync(documentPath);
                            stringDict["importedData"] = Convert.ToBase64String(bytes);
                            JsonResult = pdfviewer.ImportAnnotation(stringDict);
                            return Content(JsonConvert.SerializeObject(JsonResult));
                        }
                        else
                        {
                            var docValue = GetStringValue(jsonObject, "document") ?? importedData;
                            return this.Content(docValue + " is not found");
                        }
                    }
                }
            }
            return Content(jsonResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ImportAnnotations: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ExportFormFields")]
    public async Task<IActionResult> ExportFormFields([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            string jsonResult = pdfviewer.ExportFormFields(stringDict);
            return Content(jsonResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExportFormFields: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ImportFormFields")]
    public async Task<IActionResult> ImportFormFields([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            // Check if PDF is read-only (status = Completed)
            var document = GetStringValue(jsonObject, "document") ?? GetStringValue(jsonObject, "data");
            if (await IsPdfReadOnlyAsync(document))
            {
                return StatusCode(403, new { error = "This PDF has been approved and is read-only." });
            }

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            var dataValue = GetStringValue(jsonObject, "data");
            if (!string.IsNullOrEmpty(dataValue))
            {
                stringDict["data"] = GetDocumentPath(dataValue);
            }
            object jsonResult = pdfviewer.ImportFormFields(stringDict);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ImportFormFields: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Unload")]
    //Post action for unloading and disposing the PDF document resources  
    public async Task<IActionResult> Unload([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            pdfviewer.ClearCache(stringDict);
            return this.Content("Document cache is cleared");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Unload: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Download")]
    //Post action for downloading the PDF documents
    public async Task<IActionResult> Download([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            string documentBase = pdfviewer.GetDocumentAsBase64(stringDict);
            return Content(documentBase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Download: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Save")]
    //Post action for saving the PDF documents with annotations
    public async Task<IActionResult> Save([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            // Check if PDF is read-only (status = Completed)
            var document = GetStringValue(jsonObject, "document") ?? GetStringValue(jsonObject, "fileName");
            if (await IsPdfReadOnlyAsync(document))
            {
                return StatusCode(403, new { error = "This PDF has been approved and is read-only." });
            }

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);

            // Get document as base64 with annotations
            string documentBase64 = pdfviewer.GetDocumentAsBase64(stringDict);

            // Extract base64 data (remove data URL prefix if present)
            string base64Data = documentBase64;
            if (documentBase64.Contains(","))
            {
                base64Data = documentBase64.Substring(documentBase64.IndexOf(",") + 1);
            }

            // Decode base64 to bytes
            byte[] pdfBytes = Convert.FromBase64String(base64Data);

            // Get file path
            var fileName = GetStringValue(jsonObject, "fileName") ?? GetStringValue(jsonObject, "document");
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest(new { error = "File name is required." });
            }

            string filePath = GetDocumentPath(fileName);
            if (string.IsNullOrEmpty(filePath))
            {
                return NotFound(new { error = "File not found." });
            }

            // Write PDF with annotations to file
            await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

            Console.WriteLine($"PDF saved with annotations: {filePath}");

            return Ok(new { message = "Document saved successfully." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Save: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("PrintImages")]
    //Post action for printing the PDF documents
    public async Task<IActionResult> PrintImages([FromBody] Dictionary<string, object> jsonObject)
    {
        try
        {
            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var stringDict = ConvertToStringDictionary(jsonObject);
            object pageImage = pdfviewer.GetPrintImage(stringDict);
            return Content(JsonConvert.SerializeObject(pageImage));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in PrintImages: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Redaction and LoadFile endpoints require additional Syncfusion packages
    // (Syncfusion.Pdf.Redaction, Syncfusion.DocIO, Syncfusion.Presentation, Syncfusion.XlsIO)
    // These are commented out until the packages are installed
    /*
    [HttpPost("Redaction")]
    public IActionResult Redaction([FromBody] Dictionary<string, string> jsonObject)
    {
        // Requires Syncfusion.Pdf.Redaction package
        return BadRequest("Redaction feature requires additional Syncfusion packages");
    }

    [HttpPost("LoadFile")]
    public IActionResult LoadFile([FromBody] Dictionary<string, string> jsonObject)
    {
        // Requires Syncfusion.DocIO, Syncfusion.Presentation, Syncfusion.XlsIO packages
        return BadRequest("LoadFile feature requires additional Syncfusion packages");
    }
    */

    //Gets the path of the PDF document
    private string GetDocumentPath(string document)
    {
        if (string.IsNullOrEmpty(document))
            return string.Empty;

        // If it's already a full path and exists, use it
        if (System.IO.File.Exists(document))
        {
            return document;
        }

        var path = _hostingEnvironment.ContentRootPath;

        // Try Uploads folder first (where we save PDFs from workflows)
        var uploadsPath = Path.Combine(path, "Uploads", document);
        if (System.IO.File.Exists(uploadsPath))
        {
            return uploadsPath;
        }

        // Fallback to Data folder (for Syncfusion examples or test files)
        var dataPath = Path.Combine(path, "Data", document);
        if (System.IO.File.Exists(dataPath))
        {
            return dataPath;
        }

        Console.WriteLine($"Document path not found for: {document}");
        return string.Empty;
    }

    // Upload a stamp image
    [HttpPost("UploadStamp")]
    public async Task<IActionResult> UploadStamp(IFormFile file, [FromForm] string name = "")
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // Validate file type
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest("Invalid file type. Only image files are allowed.");
            }

            // Read file into byte array
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                // Create stamp entity
                var stamp = new Stamp
                {
                    Name = string.IsNullOrWhiteSpace(name) ? file.FileName : name,
                    ContentType = file.ContentType,
                    ImageData = imageData,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };

                _db.Stamps.Add(stamp);
                await _db.SaveChangesAsync();

                return Ok(new { id = stamp.Id, name = stamp.Name, message = "Stamp uploaded successfully." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading stamp: {ex.Message}");
        }
    }

    // Get all stamps
    [HttpGet("GetStamps")]
    public IActionResult GetStamps()
    {
        try
        {
            var stamps = _db.Stamps
                .Where(s => s.IsActive)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    contentType = s.ContentType,
                    createdAtUtc = s.CreatedAtUtc
                })
                .OrderByDescending(s => s.createdAtUtc)
                .ToList();

            return Ok(stamps);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving stamps: {ex.Message}");
        }
    }

    // GET: Get stamp image by ID
    [HttpGet("GetStamp/{id}")]
    public IActionResult GetStamp(int id)
    {
        try
        {
            var stamp = _db.Stamps.FirstOrDefault(s => s.Id == id && s.IsActive);
            if (stamp == null)
            {
                return NotFound("Stamp not found.");
            }

            return File(stamp.ImageData, stamp.ContentType, stamp.Name);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving stamp: {ex.Message}");
        }
    }

    // DELETE: Delete a stamp
    [HttpDelete("DeleteStamp/{id}")]
    public async Task<IActionResult> DeleteStamp(int id)
    {
        try
        {
            var stamp = _db.Stamps.FirstOrDefault(s => s.Id == id);
            if (stamp == null)
            {
                return NotFound("Stamp not found.");
            }

            stamp.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Stamp deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting stamp: {ex.Message}");
        }
    }

    [HttpPost("AppendPdf")]
    public IActionResult AppendPdf([FromBody] AppendPdfRequest request)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.BaseDocument)
            || string.IsNullOrWhiteSpace(request.AppendDocument))
        {
            return BadRequest("Both base and append documents must be provided.");
        }

        try
        {
            byte[] baseBytes = Convert.FromBase64String(StripPrefix(request.BaseDocument));
            byte[] appendBytes = Convert.FromBase64String(StripPrefix(request.AppendDocument));

            using var baseStream = new MemoryStream(baseBytes);
            using var appendStream = new MemoryStream(appendBytes);
            using var baseDoc = new PdfLoadedDocument(baseStream);
            using var appendDoc = new PdfLoadedDocument(appendStream);

            if (appendDoc.Pages.Count > 0)
            {
                baseDoc.ImportPageRange(appendDoc, 0, appendDoc.Pages.Count - 1);
            }

            using var output = new MemoryStream();
            baseDoc.Save(output);
            var merged = Convert.ToBase64String(output.ToArray());

            return Ok(new { mergedDocument = $"data:application/pdf;base64,{merged}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error appending PDF: {ex.Message}");
        }
    }

    private static string StripPrefix(string value)
    {
        var comma = value.IndexOf(',');
        return comma >= 0 ? value[(comma + 1)..] : value.Trim();
    }

    public class AppendPdfRequest
    {
        public string BaseDocument { get; set; } = string.Empty;
        public string AppendDocument { get; set; } = string.Empty;
    }

    // ===== Supervisor Review Endpoints =====

    [HttpPost("SendToSupervisor")]
    public async Task<IActionResult> SendToSupervisor([FromBody] SendToSupervisorRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DocumentBase64))
            {
                return BadRequest("Document data is required.");
            }

            // Strip data URL prefix if present
            string base64String = StripPrefix(request.DocumentBase64);
            byte[] documentBytes = Convert.FromBase64String(base64String);

            Document document;

            if (request.DocumentId.HasValue && request.DocumentId.Value > 0)
            {
                // Update existing document
                document = await _db.Documents.FindAsync(request.DocumentId.Value);
                if (document == null)
                {
                    return NotFound("Document not found.");
                }

                document.Content = documentBytes;
                document.DocumentBase64 = request.DocumentBase64;
                document.Status = "pending";
                document.SentToSupervisorAtUtc = DateTime.UtcNow;
                document.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                // Create new document
                document = new Document
                {
                    Name = $"Document_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                    Content = documentBytes,
                    DocumentBase64 = request.DocumentBase64,
                    Status = "pending",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    SentToSupervisorAtUtc = DateTime.UtcNow
                };
                _db.Documents.Add(document);
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                documentId = document.Id,
                status = document.Status,
                message = "Document sent to supervisor successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error sending document to supervisor: {ex.Message}");
        }
    }

    public class SendToSupervisorRequest
    {
        public string DocumentBase64 { get; set; } = string.Empty;
        public int? DocumentId { get; set; }
    }

    [HttpGet("GetDocumentStatus/{id}")]
    public IActionResult GetDocumentStatus(int id)
    {
        try
        {
            var document = _db.Documents.AsNoTracking().FirstOrDefault(d => d.Id == id);
            if (document == null)
            {
                return NotFound("Document not found.");
            }

            return Ok(new
            {
                id = document.Id,
                status = document.Status,
                documentBase64 = document.DocumentBase64,
                sentAtUtc = document.SentToSupervisorAtUtc,
                reviewedAtUtc = document.ReviewedAtUtc,
                reviewedBy = document.ReviewedBy,
                reviewComments = document.ReviewComments
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving document status: {ex.Message}");
        }
    }

    [HttpPost("ReviewDocument")]
    public async Task<IActionResult> ReviewDocument([FromBody] ReviewDocumentRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Review request is required.");
            }

            var document = await _db.Documents.FindAsync(request.DocumentId);
            if (document == null)
            {
                return NotFound("Document not found.");
            }

            if (document.Status != "pending")
            {
                return BadRequest($"Document is not in pending status. Current status: {document.Status}");
            }

            // Update document status
            if (request.Action.ToLower() == "accept")
            {
                document.Status = "accepted";
            }
            else if (request.Action.ToLower() == "reject")
            {
                if (string.IsNullOrWhiteSpace(request.Comments))
                {
                    return BadRequest("Comments are required for rejection.");
                }
                document.Status = "rejected";
            }
            else
            {
                return BadRequest("Invalid action. Use 'accept' or 'reject'.");
            }

            document.ReviewedAtUtc = DateTime.UtcNow;
            document.ReviewedBy = request.ReviewedBy ?? "Supervisor"; // You can get this from authentication context
            document.ReviewComments = request.Comments;
            document.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                documentId = document.Id,
                status = document.Status,
                message = $"Document {request.Action}ed successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reviewing document: {ex.Message}");
        }
    }

    public class ReviewDocumentRequest
    {
        public int DocumentId { get; set; }
        public string Action { get; set; } = string.Empty; // "accept" or "reject"
        public string? Comments { get; set; }
        public string? ReviewedBy { get; set; }
    }

    [HttpGet("GetPendingDocuments")]
    public IActionResult GetPendingDocuments()
    {
        try
        {
            var pendingDocuments = _db.Documents
                .AsNoTracking()
                .Where(d => d.Status == "pending")
                .OrderByDescending(d => d.SentToSupervisorAtUtc)
                .Select(d => new
                {
                    id = d.Id,
                    fileName = d.Name,
                    status = d.Status,
                    sentAtUtc = d.SentToSupervisorAtUtc,
                    createdAtUtc = d.CreatedAtUtc
                })
                .ToList();

            return Ok(pendingDocuments);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving pending documents: {ex.Message}");
        }
    }

    // POST PdfViewer/AddSignature
    [HttpPost("AddSignature")]
    public IActionResult AddSignature([FromBody] AddSignatureRequest request)
    {
        if (request == null ||
            (string.IsNullOrWhiteSpace(request.DocumentBase64) &&
             string.IsNullOrWhiteSpace(request.HashId) &&
             string.IsNullOrWhiteSpace(request.FileName)) ||
            string.IsNullOrWhiteSpace(request.SignatureBase64))
        {
            return BadRequest("Document data and signature image are required.");
        }

        // Prefer the document passed in the request; otherwise fall back to the cached viewer document.
        byte[] pdfBytes;
        if (!string.IsNullOrWhiteSpace(request.DocumentBase64))
        {
            pdfBytes = Convert.FromBase64String(StripPrefix(request.DocumentBase64));
        }
        else
        {
            var cachedDocRequest = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(request.HashId))
                cachedDocRequest["hashId"] = request.HashId;
            if (!string.IsNullOrWhiteSpace(request.FileName))
            {
                cachedDocRequest["fileName"] = request.FileName;
                cachedDocRequest["document"] = request.FileName;
            }
            if (request.DocumentId.HasValue)
                cachedDocRequest["documentId"] = request.DocumentId.Value.ToString();

            var cachedDocRequestString = ConvertToStringDictionary(cachedDocRequest);

            var cachedBase64 = new PdfRenderer(_cache).GetDocumentAsBase64(cachedDocRequestString);
            if (string.IsNullOrWhiteSpace(cachedBase64))
                return BadRequest("Unable to retrieve PDF from viewer cache. Pass DocumentBase64.");
            pdfBytes = Convert.FromBase64String(StripPrefix(cachedBase64));
        }

        using var loadedDocument = new PdfLoadedDocument(pdfBytes);
        var pageIndex = Math.Clamp(request.PageNumber - 1, 0, loadedDocument.Pages.Count - 1);
        var page = loadedDocument.Pages[pageIndex] as PdfLoadedPage ?? throw new InvalidOperationException("Page load failed.");

        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;

        var sigWidth = request.SignatureWidth ?? 120;
        var sigHeight = request.SignatureHeight ?? 45;
        var rightMargin = request.RightMargin ?? 36;
        var bottomMargin = request.BottomMargin ?? 36;

        var x = Math.Max(0, pageWidth - sigWidth - rightMargin);
        var y = Math.Max(0, pageHeight - sigHeight - bottomMargin);

        var bounds = new Syncfusion.Drawing.RectangleF((float)x, (float)y, (float)sigWidth, (float)sigHeight);
        var stamp = new PdfRubberStampAnnotation(bounds)
        {
            Author = request.Author ?? "Signature",
            Subject = "Handwritten signature",
            Opacity = 1f
        };

        // keep appearance transparent – don't draw any background before the image
        var signatureBytes = Convert.FromBase64String(StripPrefix(request.SignatureBase64));
        using (var imageStream = new MemoryStream(signatureBytes))
        {
            var image = new PdfBitmap(imageStream);
            stamp.Appearance.Normal.Graphics.DrawImage(
                image,
                new Syncfusion.Drawing.RectangleF(0, 0, bounds.Width, bounds.Height));
        }

        // lock and flatten so it can't be moved/edited
        stamp.AnnotationFlags = PdfAnnotationFlags.Locked;
        stamp.Flatten = true;

        page.Annotations.Add(stamp);

        using var output = new MemoryStream();
        loadedDocument.Save(output);
        var updatedBase64 = Convert.ToBase64String(output.ToArray());

        return Ok(new
        {
            pageNumber = pageIndex + 1,
            documentBase64 = $"data:application/pdf;base64,{updatedBase64}"
        });
    }

    public class AddSignatureRequest
    {
        public string? DocumentBase64 { get; set; }
        public string SignatureBase64 { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public double? SignatureWidth { get; set; }
        public double? SignatureHeight { get; set; }
        public double? RightMargin { get; set; }
        public double? BottomMargin { get; set; }
        public string? Author { get; set; }

        // Optional hooks if you still want to read from PdfViewer cache
        public string? HashId { get; set; }
        public string? FileName { get; set; }
        public int? DocumentId { get; set; }
    }

    // Extension method for extracting images from PdfLoadedRubberStampAnnotation
    public static class PdfLoadedRubberStampAnnotationExtension
    {
        public static Stream[] GetImages(PdfLoadedRubberStampAnnotation annotation)
        {
            List<Stream> imageStreams = new List<Stream>();
            // This is a placeholder - actual implementation would require deeper PDF structure access
            // For now, return empty array - the frontend should provide imageBase64
            return imageStreams.ToArray();
        }
    }
}
