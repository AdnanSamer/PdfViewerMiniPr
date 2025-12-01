using PdfViewrMiniPr.Domain.Common;

namespace PdfViewrMiniPr.Domain.Entities;

public class Document : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string DocumentBase64 { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, accepted, rejected
    public string? AnnotationsJson { get; set; }
    public DateTime? SentToSupervisorAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComments { get; set; }
}

