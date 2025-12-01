using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Infrastructure.Database;

namespace PdfViewrMiniPr.Infrastructure.Repositories;

public interface IWorkflowStampRepository : IRepositoryBase<WorkflowStamp>
{
}

public class WorkflowStampRepository : RepositoryBase<WorkflowStamp>, IWorkflowStampRepository
{
    public WorkflowStampRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
}



