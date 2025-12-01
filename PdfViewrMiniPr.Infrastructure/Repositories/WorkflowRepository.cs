using Microsoft.EntityFrameworkCore;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Infrastructure.Database;

namespace PdfViewrMiniPr.Infrastructure.Repositories;

public interface IWorkflowRepository : IRepositoryBase<Workflow>
{
    Task<IReadOnlyList<Workflow>> GetForInternalReviewerAsync(int reviewerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workflow>> GetForExternalEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Workflow?> GetByPdfFilePathAsync(string pdfFilePath, CancellationToken cancellationToken = default);
}

public class WorkflowRepository : RepositoryBase<Workflow>, IWorkflowRepository
{
    public WorkflowRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public override async Task<Workflow?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Workflows
            .Include(w => w.InternalReviewer)
            .Include(w => w.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Workflow>> GetForInternalReviewerAsync(int reviewerId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Workflows
            .Include(w => w.InternalReviewer)
            .Where(w => w.InternalReviewerId == reviewerId &&
                        (w.Status == WorkflowStatus.PendingInternalReview || w.Status == WorkflowStatus.InternalApproved))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Workflow>> GetForExternalEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbContext.Workflows
            .Include(w => w.InternalReviewer)
            .Where(w => w.ExternalReviewerEmail == email)
            .ToListAsync(cancellationToken);
    }

    public async Task<Workflow?> GetByPdfFilePathAsync(string pdfFilePath, CancellationToken cancellationToken = default)
    {
        // Try exact match first
        var workflow = await DbContext.Workflows
            .Include(w => w.InternalReviewer)
            .FirstOrDefaultAsync(w => w.PdfFilePath == pdfFilePath, cancellationToken);
        
        if (workflow != null)
            return workflow;

        // Try matching by filename (in case path format differs)
        var fileName = Path.GetFileName(pdfFilePath);
        if (!string.IsNullOrEmpty(fileName))
        {
            workflow = await DbContext.Workflows
                .Include(w => w.InternalReviewer)
                .FirstOrDefaultAsync(w => w.PdfFilePath.Contains(fileName), cancellationToken);
        }

        return workflow;
    }
}



