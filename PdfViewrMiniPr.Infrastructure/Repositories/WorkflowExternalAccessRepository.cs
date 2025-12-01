using Microsoft.EntityFrameworkCore;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Infrastructure.Database;

namespace PdfViewrMiniPr.Infrastructure.Repositories;

public interface IWorkflowExternalAccessRepository : IRepositoryBase<WorkflowExternalAccess>
{
    Task<WorkflowExternalAccess?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
}

public class WorkflowExternalAccessRepository : RepositoryBase<WorkflowExternalAccess>, IWorkflowExternalAccessRepository
{
    public WorkflowExternalAccessRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<WorkflowExternalAccess?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return DbContext.WorkflowExternalAccesses.FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
    }
}



