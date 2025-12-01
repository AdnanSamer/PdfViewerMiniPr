using PdfViewrMiniPr.Domain.Common;

namespace PdfViewrMiniPr.Domain.Entities;

public class WorkflowExternalAccess : BaseEntity
{
    public int WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = default!;

    public string Token { get; set; } = default!;

    public string OtpHash { get; set; } = default!;

    public DateTime ExpiresAtUtc { get; set; }

    public bool Used { get; set; }

    public DateTime? UsedAtUtc { get; set; }
}



