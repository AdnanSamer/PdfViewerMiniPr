using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;

namespace PdfViewerMiniPr.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<WorkflowSummaryDto>> Create(
        [FromQuery] int currentUserId,
        [FromForm] CreateWorkflowForm form,
        CancellationToken cancellationToken)
    {
        if (form.File == null || form.File.Length == 0)
        {
            return BadRequest("PDF file is required.");
        }

        var dto = new CreateWorkflowDto
        {
            Title = form.Title,
            InternalReviewerId = form.InternalReviewerId,
            ExternalReviewerEmail = form.ExternalReviewerEmail
        };

        var result = await _workflowService.CreateWorkflowAsync(currentUserId, dto, form.File, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowSummaryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.GetByIdAsync(id, cancellationToken);
        if (workflow == null)
        {
            return NotFound();
        }
        return Ok(workflow);
    }

    public class CreateWorkflowForm
    {
        public string Title { get; set; } = default!;
        public int InternalReviewerId { get; set; }
        public string ExternalReviewerEmail { get; set; } = default!;
        public IFormFile File { get; set; } = default!;
    }
}



