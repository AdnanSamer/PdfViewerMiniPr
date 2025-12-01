using PdfViewrMiniPr.Aplication.DTOs;

namespace PdfViewrMiniPr.Aplication.Interfaces;

public interface IInternalReviewService
{
    Task ApproveInternalAsync(int reviewerUserId, InternalReviewApprovalDto dto, CancellationToken cancellationToken = default);
}



