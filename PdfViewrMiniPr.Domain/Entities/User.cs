using PdfViewrMiniPr.Domain.Common;
using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewrMiniPr.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Workflow> InitiatedWorkflows { get; set; } = new List<Workflow>();

    public ICollection<Workflow> InternalReviews { get; set; } = new List<Workflow>();

    public ICollection<WorkflowExternalAccess> ExternalAccesses { get; set; } = new List<WorkflowExternalAccess>();
}



