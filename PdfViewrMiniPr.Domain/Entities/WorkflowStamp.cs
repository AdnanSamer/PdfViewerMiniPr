using PdfViewrMiniPr.Domain.Common;

namespace PdfViewrMiniPr.Domain.Entities;

public class WorkflowStamp : BaseEntity
{
    public int WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = default!;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string Label { get; set; } = default!;

    public int PageNumber { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}



