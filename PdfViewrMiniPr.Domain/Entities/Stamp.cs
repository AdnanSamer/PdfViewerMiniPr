using PdfViewrMiniPr.Domain.Common;

namespace PdfViewrMiniPr.Domain.Entities;

public class Stamp : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
}

