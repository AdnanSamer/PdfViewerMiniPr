using PdfViewrMiniPr.Aplication.DTOs;

namespace PdfViewrMiniPr.Aplication.Interfaces;

public interface IExternalReviewService
{
    Task<bool> ValidateOtpAsync(ExternalOtpValidationDto dto, CancellationToken cancellationToken = default);
    Task ApproveExternalAsync(ExternalApprovalDto dto, CancellationToken cancellationToken = default);
    Task<WorkflowSummaryDto?> GetWorkflowByTokenAsync(string token, int? workflowId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WorkflowSummaryDto>? Workflows, string? ErrorCode)> GetWorkflowsByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<ExternalUserInfoDto?> GetExternalUserInfoByTokenAsync(string token, CancellationToken cancellationToken = default);
}



