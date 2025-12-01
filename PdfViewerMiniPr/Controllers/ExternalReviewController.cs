using Microsoft.AspNetCore.Mvc;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;

namespace PdfViewerMiniPr.Controllers;

[ApiController]
[Route("api/external-review")]
public class ExternalReviewController : ControllerBase
{
    private readonly IExternalReviewService _externalReviewService;

    public ExternalReviewController(IExternalReviewService externalReviewService)
    {
        _externalReviewService = externalReviewService;
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
}



