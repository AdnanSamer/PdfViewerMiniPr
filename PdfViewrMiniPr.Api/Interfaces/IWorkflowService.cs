using Microsoft.AspNetCore.Http;
using PdfViewrMiniPr.Aplication.DTOs;

namespace PdfViewrMiniPr.Aplication.Interfaces;

public interface IWorkflowService
{
    Task<WorkflowSummaryDto> CreateWorkflowAsync(
        int currentUserId,
        CreateWorkflowDto dto,
        IFormFile pdfFile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowSummaryDto>> GetForInternalReviewerAsync(
        int reviewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowSummaryDto>> GetForExternalEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<WorkflowSummaryDto?> GetByIdAsync(int workflowId, CancellationToken cancellationToken = default);
}



