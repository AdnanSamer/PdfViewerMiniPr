namespace PdfViewrMiniPr.Domain.Enums
{
    public enum UserRole
    {
        Admin = 1,
        InternalUser = 2,
        ExternalUser = 3
    }

    public enum WorkflowStatus
    {
        Draft = 0,
        PendingInternalReview = 1,
        InternalApproved = 2,
        PendingExternalReview = 3,
        Completed = 4,
        Rejected = 5
    }
}

