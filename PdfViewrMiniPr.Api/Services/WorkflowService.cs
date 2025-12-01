using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Infrastructure.Repositories;

namespace PdfViewrMiniPr.Aplication.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _env;

    public WorkflowService(
        IWorkflowRepository workflowRepository,
        IUserRepository userRepository,
        IWebHostEnvironment env)
    {
        _workflowRepository = workflowRepository;
        _userRepository = userRepository;
        _env = env;
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



