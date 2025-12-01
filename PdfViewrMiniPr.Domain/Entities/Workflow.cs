using PdfViewrMiniPr.Domain.Common;
using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewrMiniPr.Domain.Entities;

public class Workflow : BaseEntity
{
    public string Title { get; set; } = default!;

    public string PdfFilePath { get; set; } = default!;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public int InternalReviewerId { get; set; }
    public User InternalReviewer { get; set; } = default!;

    public string ExternalReviewerEmail { get; set; } = default!;

    public WorkflowStatus Status { get; set; } = WorkflowStatus.PendingInternalReview;

    public DateTime? InternalApprovedAtUtc { get; set; }

    public DateTime? ExternalApprovedAtUtc { get; set; }

    public ICollection<WorkflowStamp> Stamps { get; set; } = new List<WorkflowStamp>();

    public ICollection<WorkflowExternalAccess> ExternalAccesses { get; set; } = new List<WorkflowExternalAccess>();
}



