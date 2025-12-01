namespace PdfViewrMiniPr.Domain.Interfaces;

public interface IPdfStampService
{
    Task ApplyStampAsync(
        string pdfPath,
        string label,
        int pageNumber,
        float x,
        float y,
        CancellationToken cancellationToken = default);
}



