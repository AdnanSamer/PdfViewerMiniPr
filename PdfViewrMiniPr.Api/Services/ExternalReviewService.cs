using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Domain.Interfaces;
using PdfViewrMiniPr.Infrastructure.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewrMiniPr.Aplication.Services;

public class ExternalReviewService : IExternalReviewService
{
    private readonly IWorkflowExternalAccessRepository _externalAccessRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IWorkflowStampRepository _stampRepository;

    public ExternalReviewService(
        IWorkflowExternalAccessRepository externalAccessRepository,
        IWorkflowRepository workflowRepository,
        IWorkflowStampRepository stampRepository)
    {
        _externalAccessRepository = externalAccessRepository;
        _workflowRepository = workflowRepository;
        _stampRepository = stampRepository;
    }

    public async Task<WorkflowSummaryDto?> GetWorkflowByTokenAsync(string token, int? workflowId = null, CancellationToken cancellationToken = default)
    {
        // Validate token
        var access = await _externalAccessRepository.GetByTokenAsync(token, cancellationToken);
        if (access == null || access.ExpiresAtUtc < DateTime.UtcNow)
        {
            return null;
        }

        // If workflowId is provided, get that specific workflow
        // Otherwise, get the workflow associated with the token
        int targetWorkflowId = workflowId ?? access.WorkflowId;
        
        var workflow = await _workflowRepository.GetByIdAsync(targetWorkflowId, cancellationToken);
        if (workflow == null)
        {
            return null;
        }

        // Validate that the workflow belongs to the same external reviewer email as the token
        var tokenWorkflow = await _workflowRepository.GetByIdAsync(access.WorkflowId, cancellationToken);
        if (tokenWorkflow == null || 
            string.IsNullOrWhiteSpace(tokenWorkflow.ExternalReviewerEmail) ||
            workflow.ExternalReviewerEmail != tokenWorkflow.ExternalReviewerEmail)
        {
            return null; // Workflow doesn't belong to the same external reviewer
        }

        return new WorkflowSummaryDto
        {
            Id = workflow.Id,
            Title = workflow.Title,
            Status = workflow.Status,
            InternalReviewerName = workflow.InternalReviewer?.FullName ?? "Unknown",
            ExternalReviewerEmail = workflow.ExternalReviewerEmail,
            PdfFilePath = workflow.PdfFilePath,
            PdfFileName = Path.GetFileName(workflow.PdfFilePath)
        };
    }

    public async Task<(IReadOnlyList<WorkflowSummaryDto>? Workflows, string? ErrorCode)> GetWorkflowsByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // Validate token format
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, "INVALID_TOKEN");
        }

        // Validate token exists
        var access = await _externalAccessRepository.GetByTokenAsync(token, cancellationToken);
        if (access == null)
        {
            return (null, "TOKEN_NOT_FOUND");
        }

        // Check if token is expired
        if (access.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (null, "TOKEN_EXPIRED");
        }

        // Get the workflow associated with this token to find the external reviewer email
        var tokenWorkflow = await _workflowRepository.GetByIdAsync(access.WorkflowId, cancellationToken);
        if (tokenWorkflow == null || string.IsNullOrWhiteSpace(tokenWorkflow.ExternalReviewerEmail))
        {
            return (null, "WORKFLOW_NOT_FOUND");
        }

        // Get all workflows for this external reviewer email
        var workflows = await _workflowRepository.GetForExternalEmailAsync(tokenWorkflow.ExternalReviewerEmail, cancellationToken);

        // FILTER: Only return status 3 (PendingExternalReview) and 4 (Completed)
        // External users should only see workflows they can review or have already reviewed
        var filteredWorkflows = workflows
            .Where(w => w.Status == WorkflowStatus.PendingExternalReview || 
                       w.Status == WorkflowStatus.Completed)
            .ToList();

        // Map to DTOs
        var workflowDtos = filteredWorkflows.Select(w => new WorkflowSummaryDto
        {
            Id = w.Id,
            Title = w.Title,
            Status = w.Status,
            InternalReviewerName = w.InternalReviewer?.FullName ?? "Unknown",
            ExternalReviewerEmail = w.ExternalReviewerEmail,
            PdfFilePath = w.PdfFilePath,
            PdfFileName = Path.GetFileName(w.PdfFilePath)
        }).ToList();

        return (workflowDtos, null);
    }

    public async Task<bool> ValidateOtpAsync(ExternalOtpValidationDto dto, CancellationToken cancellationToken = default)
    {
        var access = await _externalAccessRepository.GetByTokenAsync(dto.Token, cancellationToken);
        if (access == null || access.Used || access.ExpiresAtUtc < DateTime.UtcNow)
        {
            return false;
        }

        var otpHash = Hash(dto.Otp);
        return otpHash == access.OtpHash;
    }

    public async Task ApproveExternalAsync(ExternalApprovalDto dto, CancellationToken cancellationToken = default)
    {
        // Validate token
        var access = await _externalAccessRepository.GetByTokenAsync(dto.Token, cancellationToken)
                     ?? throw new InvalidOperationException("Invalid token.");

        if (access.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Token expired.");
        }

        // Determine which workflow to approve
        int targetWorkflowId = dto.WorkflowId ?? access.WorkflowId;
        
        var workflow = await _workflowRepository.GetByIdAsync(targetWorkflowId, cancellationToken)
                       ?? throw new InvalidOperationException("Workflow not found.");

        // Validate that the workflow belongs to the same external reviewer email as the token
        var tokenWorkflow = await _workflowRepository.GetByIdAsync(access.WorkflowId, cancellationToken);
        if (tokenWorkflow == null || 
            string.IsNullOrWhiteSpace(tokenWorkflow.ExternalReviewerEmail) ||
            workflow.ExternalReviewerEmail != tokenWorkflow.ExternalReviewerEmail)
        {
            throw new InvalidOperationException("Workflow does not belong to this external reviewer.");
        }

        if (workflow.Status != WorkflowStatus.PendingExternalReview)
        {
            throw new InvalidOperationException($"Workflow is not pending external review. Current status: {workflow.Status}");
        }

        // Save stamp metadata if provided
        if (dto.Stamp != null)
        {
            var stamp = new Domain.Entities.WorkflowStamp
            {
                WorkflowId = workflow.Id,
                UserId = null, 
                Label = dto.Stamp.Label,
                PageNumber = dto.Stamp.PageNumber,
                X = dto.Stamp.X,
                Y = dto.Stamp.Y
            };
            await _stampRepository.AddAsync(stamp, cancellationToken);
        }

        // Update workflow status to Completed
        workflow.Status = WorkflowStatus.Completed;
        workflow.ExternalApprovedAtUtc = DateTime.UtcNow;

        await _workflowRepository.SaveChangesAsync(cancellationToken);
        await _stampRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExternalUserInfoDto?> GetExternalUserInfoByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var access = await _externalAccessRepository.GetByTokenAsync(token, cancellationToken);
        if (access == null || access.ExpiresAtUtc < DateTime.UtcNow)
        {
            return null;
        }

        var workflow = await _workflowRepository.GetByIdAsync(access.WorkflowId, cancellationToken);
        if (workflow == null || string.IsNullOrWhiteSpace(workflow.ExternalReviewerEmail))
        {
            return null;
        }

        return new ExternalUserInfoDto
        {
            Email = workflow.ExternalReviewerEmail,
            IsValid = true
        };
    }

    private async Task ApplyAnnotationsToPdfAsync(Domain.Entities.Workflow workflow, string annotationsJson, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(annotationsJson) || !System.IO.File.Exists(workflow.PdfFilePath))
            {
                return;
            }

            // Load the PDF file
            byte[] pdfBytes = await System.IO.File.ReadAllBytesAsync(workflow.PdfFilePath, cancellationToken);
            
            // The actual annotation application will be handled by the Save endpoint
            // which is called by the frontend before approval
        }
        catch (Exception)
        {
            // Don't fail approval if annotation application fails
        }
    }

    private static string Hash(string value)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }
}


