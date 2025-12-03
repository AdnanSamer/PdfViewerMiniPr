using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Domain.Interfaces;
using PdfViewrMiniPr.Infrastructure.Repositories;

namespace PdfViewrMiniPr.Aplication.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public WorkflowService(
        IWorkflowRepository workflowRepository,
        IUserRepository userRepository,
        IWebHostEnvironment env,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _workflowRepository = workflowRepository;
        _userRepository = userRepository;
        _env = env;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<WorkflowSummaryDto> CreateWorkflowAsync(
        int currentUserId,
        CreateWorkflowDto dto,
        IFormFile pdfFile,
        CancellationToken cancellationToken = default)
    {
        var creator = await _userRepository.GetByIdAsync(currentUserId, cancellationToken)
                      ?? throw new InvalidOperationException("Creator user not found.");

        var reviewer = await _userRepository.GetByIdAsync(dto.InternalReviewerId, cancellationToken)
                       ?? throw new InvalidOperationException("Internal reviewer not found.");

        var filePath = await SavePdfAsync(pdfFile, cancellationToken);

        var workflow = new Workflow
        {
            Title = dto.Title,
            CreatedByUserId = creator.Id,
            InternalReviewerId = reviewer.Id,
            ExternalReviewerEmail = dto.ExternalReviewerEmail,
            PdfFilePath = filePath
        };

        await _workflowRepository.AddAsync(workflow, cancellationToken);
        await _workflowRepository.SaveChangesAsync(cancellationToken);

        // Send internal-review email with login + returnUrl link
        try
        {
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var reviewLink = $"{frontendBaseUrl.TrimEnd('/')}/login?returnUrl=/internal/review/{workflow.Id}";

            var subject = "New document assigned for internal review";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: #ffffff; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .button:hover {{ background-color: #0056b3; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Internal Review Request</h2>
        <p>A new document has been assigned to you for internal review.</p>
        <p><strong>Title:</strong> {workflow.Title}</p>
        <p style=""text-align: center;"">
            <a href=""{reviewLink}"" class=""button"">Open Review</a>
        </p>
        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #666;"">{reviewLink}</p>
    </div>
</body>
</html>";

            await _emailService.SendAsync(
                reviewer.Email,
                subject,
                body,
                cancellationToken);
        }
        catch
        {
            // Ignore email errors; workflow creation should not fail because of email issues
        }

        return new WorkflowSummaryDto
        {
            Id = workflow.Id,
            Title = workflow.Title,
            Status = workflow.Status,
            InternalReviewerName = reviewer.FullName,
            ExternalReviewerEmail = workflow.ExternalReviewerEmail,
            PdfFilePath = workflow.PdfFilePath,
            PdfFileName = Path.GetFileName(workflow.PdfFilePath)
        };
    }

    public async Task<WorkflowSummaryDto?> GetByIdAsync(int workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow == null)
            return null;

        // Get reviewer name - safely handle null navigation property
        string reviewerName = "Unknown";
        try
        {
            if (workflow.InternalReviewer != null)
            {
                reviewerName = workflow.InternalReviewer.FullName;
            }
            else if (workflow.InternalReviewerId > 0)
            {
                // Fallback: fetch reviewer separately if navigation property wasn't loaded
                var reviewer = await _userRepository.GetByIdAsync(workflow.InternalReviewerId, cancellationToken);
                if (reviewer != null)
                {
                    reviewerName = reviewer.FullName;
                }
            }
        }
        catch
        {
            // If anything fails, try to fetch reviewer separately
            if (workflow.InternalReviewerId > 0)
            {
                var reviewer = await _userRepository.GetByIdAsync(workflow.InternalReviewerId, cancellationToken);
                if (reviewer != null)
                {
                    reviewerName = reviewer.FullName;
                }
            }
        }

        return new WorkflowSummaryDto
        {
            Id = workflow.Id,
            Title = workflow.Title,
            Status = workflow.Status,
            InternalReviewerName = reviewerName,
            ExternalReviewerEmail = workflow.ExternalReviewerEmail,
            PdfFilePath = workflow.PdfFilePath,
            PdfFileName = Path.GetFileName(workflow.PdfFilePath)
        };
    }

    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetForInternalReviewerAsync(
        int reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var items = await _workflowRepository.GetForInternalReviewerAsync(reviewerUserId, cancellationToken);
        return items.Select(w => new WorkflowSummaryDto
        {
            Id = w.Id,
            Title = w.Title,
            Status = w.Status,
            InternalReviewerName = w.InternalReviewer?.FullName ?? "Unknown",
            ExternalReviewerEmail = w.ExternalReviewerEmail,
            PdfFilePath = w.PdfFilePath,
            PdfFileName = Path.GetFileName(w.PdfFilePath)
        }).ToList();
    }

    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetForExternalEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var items = await _workflowRepository.GetForExternalEmailAsync(email, cancellationToken);
        return items.Select(w => new WorkflowSummaryDto
        {
            Id = w.Id,
            Title = w.Title,
            Status = w.Status,
            InternalReviewerName = w.InternalReviewer?.FullName ?? "Unknown",
            ExternalReviewerEmail = w.ExternalReviewerEmail,
            PdfFilePath = w.PdfFilePath,
            PdfFileName = Path.GetFileName(w.PdfFilePath)
        }).ToList();
    }

    private async Task<string> SavePdfAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "Uploads");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return fullPath;
    }
}



