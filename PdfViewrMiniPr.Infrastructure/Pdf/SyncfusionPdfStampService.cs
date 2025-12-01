using PdfViewrMiniPr.Domain.Interfaces;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;

namespace PdfViewrMiniPr.Infrastructure.Pdf;

public class SyncfusionPdfStampService : IPdfStampService
{
    public Task ApplyStampAsync(
        string pdfPath,
        string label,
        int pageNumber,
        float x,
        float y,
        CancellationToken cancellationToken = default)
    {
        // Load existing PDF from disk into memory
        var bytes = File.ReadAllBytes(pdfPath);
        using var inputStream = new MemoryStream(bytes);
        using var document = new PdfLoadedDocument(inputStream);

        var index = Math.Max(0, Math.Min(pageNumber - 1, document.Pages.Count - 1));
        var page = document.Pages[index];

        var graphics = page.Graphics;
        var font = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
        var brush = PdfBrushes.Red;

        graphics.DrawString(label, font, brush, x, y);

        // Save back to the same file
        using var outputStream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write);
        document.Save(outputStream);
        document.Close(true);

        return Task.CompletedTask;
    }
}


