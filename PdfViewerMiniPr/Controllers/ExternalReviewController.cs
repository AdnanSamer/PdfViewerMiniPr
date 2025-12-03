using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Infrastructure.Repositories;
using System.Security.Claims;
using System.IO;

namespace PdfViewerMiniPr.Controllers;

[ApiController]
[Route("api/external-review")]
public class ExternalReviewController : ControllerBase
{
    private readonly IExternalReviewService _externalReviewService;
    private readonly IWorkflowRepository _workflowRepository;

    public ExternalReviewController(
        IExternalReviewService externalReviewService,
        IWorkflowRepository workflowRepository)
    {
        _externalReviewService = externalReviewService;
        _workflowRepository = workflowRepository;
    }

    [HttpPost("validate-otp")]
    public async Task<ActionResult<bool>> ValidateOtp(
        [FromBody] ExternalOtpValidationDto dto,
        CancellationToken cancellationToken)
    {
        var isValid = await _externalReviewService.ValidateOtpAsync(dto, cancellationToken);
        return Ok(isValid);
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve(
        [FromBody] ExternalApprovalDto dto,
        CancellationToken cancellationToken = default)
    {
        // Token is required for the email-link flow
        if (string.IsNullOrWhiteSpace(dto?.Token))
        {
            return BadRequest(new { error = "Token is required for the email-link approval flow." });
        }

        try
        {
            await _externalReviewService.ApproveExternalAsync(dto, cancellationToken);
            return Ok(new { message = "Workflow approved successfully." });
        }
        catch (InvalidOperationException ex)
        {
            // Return appropriate status code based on error message
            if (ex.Message.Contains("does not belong"))
            {
                return StatusCode(403, new { error = ex.Message });
            }
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("user-info")]
    public async Task<ActionResult<ExternalUserInfoDto>> GetExternalUserInfo(
        [FromQuery] string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token is required." });
        }

        var userInfo = await _externalReviewService.GetExternalUserInfoByTokenAsync(token, cancellationToken);
        if (userInfo == null)
        {
            return NotFound(new { error = "Invalid or expired token." });
        }

        return Ok(userInfo);
    }

    // ===== JWT-based endpoints for logged-in ExternalUser =====

    /// <summary>
    /// Get external user info for the currently authenticated ExternalUser (JWT-based).
    /// </summary>
    [Authorize(Roles = "ExternalUser")]
    [HttpGet("user-info/current")]
    public ActionResult<ExternalUserInfoDto> GetCurrentUserInfo()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new { error = "Email claim is missing from JWT." });
        }

        return Ok(new ExternalUserInfoDto
        {
            Email = email,
            IsValid = true
        });
    }

    [HttpGet("workflow")]
    public async Task<ActionResult<WorkflowSummaryDto>> GetWorkflowByToken(
        [FromQuery] string token,
        [FromQuery] int? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token is required." });
        }

        var workflow = await _externalReviewService.GetWorkflowByTokenAsync(token, workflowId, cancellationToken);
        if (workflow == null)
        {
            return NotFound(new { error = "Invalid or expired token, or workflow not found." });
        }
        return Ok(workflow);
    }

    /// <summary>
    /// Token-based workflows endpoint (email-link flow, kept for compatibility).
    /// </summary>
    [HttpGet("workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> GetWorkflowsByToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token is required." });
        }

        var (workflows, errorCode) = await _externalReviewService.GetWorkflowsByTokenAsync(token, cancellationToken);
        
        if (errorCode != null)
        {
            return errorCode switch
            {
                "INVALID_TOKEN" => BadRequest(new { error = "Invalid token format." }),
                "TOKEN_NOT_FOUND" => NotFound(new { error = "Token not found." }),
                "TOKEN_EXPIRED" => Unauthorized(new { error = "Token has expired." }),
                "WORKFLOW_NOT_FOUND" => NotFound(new { error = "Workflow associated with token not found." }),
                _ => BadRequest(new { error = "Invalid or expired token." })
            };
        }

        if (workflows == null || workflows.Count == 0)
        {
            return NotFound(new { error = "No workflows found for this token." });
        }

        return Ok(workflows);
    }

    /// <summary>
    /// JWT-based workflows endpoint for the currently authenticated ExternalUser.
    /// Lists workflows for the user's email, filtered to PendingExternalReview and Completed.
    /// </summary>
    [Authorize(Roles = "ExternalUser")]
    [HttpGet("workflows/current")]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> GetWorkflowsForCurrentUser(
        CancellationToken cancellationToken)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new { error = "Email claim is missing from JWT." });
        }

        var workflows = await _workflowRepository.GetForExternalEmailAsync(email, cancellationToken);

        var filtered = workflows
            .Where(w => w.Status == WorkflowStatus.PendingExternalReview ||
                        w.Status == WorkflowStatus.Completed)
            .Select(w => new WorkflowSummaryDto
            {
                Id = w.Id,
                Title = w.Title,
                Status = w.Status,
                InternalReviewerName = w.InternalReviewer?.FullName ?? string.Empty,
                ExternalReviewerEmail = w.ExternalReviewerEmail,
                PdfFilePath = w.PdfFilePath,
                PdfFileName = Path.GetFileName(w.PdfFilePath)
            })
            .ToList();

        if (filtered.Count == 0)
        {
            return Ok(Array.Empty<WorkflowSummaryDto>());
        }

        return Ok(filtered);
    }

    /// <summary>
    /// Optional: Approve a workflow for the currently authenticated ExternalUser using JWT only.
    /// No token required; validates that the workflow belongs to this external email.
    /// </summary>
    [Authorize(Roles = "ExternalUser")]
    [HttpPost("approve-current")]
    public async Task<IActionResult> ApproveForCurrentUser(
        [FromBody] ExternalApprovalDto dto,
        CancellationToken cancellationToken = default)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new { error = "Email claim is missing from JWT." });
        }

        if (dto == null || !dto.WorkflowId.HasValue)
        {
            return BadRequest(new { error = "WorkflowId is required." });
        }

        var workflowId = dto.WorkflowId.Value;
        var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow == null)
        {
            return NotFound(new { error = "Workflow not found." });
        }

        if (!string.Equals(workflow.ExternalReviewerEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { error = "This workflow does not belong to the current external user." });
        }

        if (workflow.Status != WorkflowStatus.PendingExternalReview)
        {
            return BadRequest(new { error = $"Workflow is not pending external review. Current status: {workflow.Status}" });
        }

        workflow.Status = WorkflowStatus.Completed;
        workflow.ExternalApprovedAtUtc = DateTime.UtcNow;

        await _workflowRepository.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Workflow approved successfully." });
    }
}



