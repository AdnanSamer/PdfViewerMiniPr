using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewrMiniPr.Aplication.DTOs;

public class WorkflowSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public WorkflowStatus Status { get; set; }
    public string InternalReviewerName { get; set; } = default!;
    public string ExternalReviewerEmail { get; set; } = default!;
    public string? PdfFilePath { get; set; }
    public string? PdfFileName { get; set; }
}

public class CreateWorkflowDto
{
    public string Title { get; set; } = default!;
    public int InternalReviewerId { get; set; }
    public string ExternalReviewerEmail { get; set; } = default!;
}

public class StampDto
{
    public string Label { get; set; } = default!;
    public int PageNumber { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

public class InternalReviewApprovalDto
{
    public int WorkflowId { get; set; }
    public StampDto Stamp { get; set; } = default!;
}

public class ExternalOtpValidationDto
{
    public string Token { get; set; } = default!;
    public string Otp { get; set; } = default!;
}

public class ExternalApprovalDto
{
    public string Token { get; set; } = default!;
    public int? WorkflowId { get; set; } // Optional: if not provided, uses the token's workflow
    public StampDto? Stamp { get; set; } // Optional: if not provided, just approves without stamp
    public string? AnnotationsJson { get; set; } // Optional: annotations from PDF viewer to persist
}

public class ExternalUserInfoDto
{
    public string Email { get; set; } = default!;
    public bool IsValid { get; set; }
}



