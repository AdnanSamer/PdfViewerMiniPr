using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;

namespace PdfViewerMiniPr.Controllers;

[ApiController]
[Route("api/internal-review")]
[Authorize] // Require authentication
public class InternalReviewController : ControllerBase
{
    private readonly IInternalReviewService _internalReviewService;
    private readonly IWorkflowService _workflowService;

    public InternalReviewController(
        IInternalReviewService internalReviewService,
        IWorkflowService workflowService)
    {
        _internalReviewService = internalReviewService;
        _workflowService = workflowService;
    }

    [HttpGet("assigned")]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> GetAssigned(
        [FromQuery] int? reviewerUserId, // Optional: for backward compatibility
        CancellationToken cancellationToken)
    {
        // Get user ID from JWT token (more secure)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Use JWT user ID if available, otherwise fall back to query parameter
        int currentUserId = 0;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var jwtUserId))
        {
            currentUserId = jwtUserId;
        }
        else if (reviewerUserId.HasValue && reviewerUserId.Value > 0)
        {
            currentUserId = reviewerUserId.Value;
        }
        else
        {
            return Unauthorized(new { error = "User ID is required. Please ensure you are authenticated." });
        }

        var items = await _workflowService.GetForInternalReviewerAsync(currentUserId, cancellationToken);
        return Ok(items);
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve(
        [FromQuery] int? reviewerUserId, // Optional: for backward compatibility
        [FromBody] InternalReviewApprovalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get user ID from JWT token (more secure)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            
            // Use JWT user ID if available, otherwise fall back to query parameter
            int currentUserId = 0;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var jwtUserId))
            {
                currentUserId = jwtUserId;
            }
            else if (reviewerUserId.HasValue && reviewerUserId.Value > 0)
            {
                currentUserId = reviewerUserId.Value;
            }
            else
            {
                return Unauthorized(new { error = "User ID is required. Please ensure you are authenticated." });
            }

            Console.WriteLine($"Approval request - UserId: {currentUserId}, Role: {roleClaim}, WorkflowId: {dto.WorkflowId}");

            await _internalReviewService.ApproveInternalAsync(currentUserId, dto, cancellationToken);
            return Ok(new { message = "Workflow approved successfully." });
        }
        catch (InvalidOperationException ex)
        {
            // Return 400 Bad Request for business logic errors
            Console.WriteLine($"Business logic error in Approve: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Return 500 for unexpected errors
            Console.WriteLine($"Unexpected error in Approve: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { error = "An unexpected error occurred while approving the workflow." });
        }
    }
}



