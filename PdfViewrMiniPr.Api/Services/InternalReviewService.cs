using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Domain.Interfaces;
using PdfViewrMiniPr.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewrMiniPr.Aplication.Services;

public class InternalReviewService : IInternalReviewService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IWorkflowStampRepository _stampRepository;
    private readonly IWorkflowExternalAccessRepository _externalAccessRepository;
    private readonly IPdfStampService _pdfStampService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public InternalReviewService(
        IWorkflowRepository workflowRepository,
        IWorkflowStampRepository stampRepository,
        IWorkflowExternalAccessRepository externalAccessRepository,
        IPdfStampService pdfStampService,
        IEmailService emailService,
        IConfiguration configuration,
        IUserRepository userRepository)
    {
        _workflowRepository = workflowRepository;
        _stampRepository = stampRepository;
        _externalAccessRepository = externalAccessRepository;
        _pdfStampService = pdfStampService;
        _emailService = emailService;
        _configuration = configuration;
        _userRepository = userRepository;
    }

    public async Task ApproveInternalAsync(int reviewerUserId, InternalReviewApprovalDto dto, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"ApproveInternalAsync - ReviewerUserId: {reviewerUserId}, WorkflowId: {dto.WorkflowId}");

        var workflow = await _workflowRepository.GetByIdAsync(dto.WorkflowId, cancellationToken)
                       ?? throw new InvalidOperationException("Workflow not found.");

        Console.WriteLine($"Workflow found - InternalReviewerId: {workflow.InternalReviewerId}, Status: {workflow.Status}");

        // Get user to check role
        var user = await _userRepository.GetByIdAsync(reviewerUserId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {reviewerUserId} not found.");
        }

        bool isAssignedReviewer = workflow.InternalReviewerId == reviewerUserId;
        bool isAdmin = user.Role == UserRole.Admin;
        bool isInternalUser = user.Role == UserRole.InternalUser;

        Console.WriteLine($"User check - IsAssignedReviewer: {isAssignedReviewer}, IsAdmin: {isAdmin}, IsInternalUser: {isInternalUser}, UserRole: {user.Role}");

        // Check authorization: User must be assigned reviewer OR Admin OR InternalUser (with reassignment)
        if (!isAssignedReviewer && !isAdmin && !isInternalUser)
        {
            throw new InvalidOperationException(
                $"You are not authorized to approve this workflow. " +
                $"Workflow is assigned to Internal Reviewer ID: {workflow.InternalReviewerId}, " +
                $"but you are User ID: {reviewerUserId} ({user.FullName}, Role: {user.Role}). " +
                $"Only internal reviewers or an Admin can approve this workflow.");
        }

        // If user is not the assigned reviewer but is an InternalUser or Admin, reassign the workflow
        if (!isAssignedReviewer && (isInternalUser || isAdmin))
        {
            Console.WriteLine($"Reassigning workflow {workflow.Id} from InternalReviewerId {workflow.InternalReviewerId} to {reviewerUserId} ({user.FullName})");
            workflow.InternalReviewerId = reviewerUserId;
            // Note: We don't save here yet, it will be saved when we update the workflow status
        }

        if (workflow.Status != WorkflowStatus.PendingInternalReview)
        {
            throw new InvalidOperationException($"Workflow is not in pending internal review state. Current status: {workflow.Status}");
        }

        // Save stamp in DB
        var stamp = new Domain.Entities.WorkflowStamp
        {
            WorkflowId = workflow.Id,
            UserId = reviewerUserId,
            Label = dto.Stamp.Label,
            PageNumber = dto.Stamp.PageNumber,
            X = dto.Stamp.X,
            Y = dto.Stamp.Y
        };
        await _stampRepository.AddAsync(stamp, cancellationToken);

        // Apply stamp to PDF
        await _pdfStampService.ApplyStampAsync(
            workflow.PdfFilePath,
            dto.Stamp.Label,
            dto.Stamp.PageNumber,
            dto.Stamp.X,
            dto.Stamp.Y,
            cancellationToken);

        // Generate token + OTP
        var token = Guid.NewGuid().ToString("N");
        var otp = GenerateOtp();
        var otpHash = Hash(otp);

        var externalAccess = new Domain.Entities.WorkflowExternalAccess
        {
            WorkflowId = workflow.Id,
            Token = token,
            OtpHash = otpHash,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };
        await _externalAccessRepository.AddAsync(externalAccess, cancellationToken);

        workflow.Status = WorkflowStatus.PendingExternalReview;
        workflow.InternalApprovedAtUtc = DateTime.UtcNow;

        await _workflowRepository.SaveChangesAsync(cancellationToken);
        await _stampRepository.SaveChangesAsync(cancellationToken);
        await _externalAccessRepository.SaveChangesAsync(cancellationToken);

        // Try to send email to external reviewer, but don't fail the workflow if email sending has issues
        try
        {
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var approvalLink = $"{frontendBaseUrl}/external-review?token={token}";
            
            // Create HTML email body with clickable link
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
        .otp-box {{ background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; text-align: center; }}
        .otp-code {{ font-size: 24px; font-weight: bold; color: #007bff; letter-spacing: 5px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Document Review Request</h2>
        <p>You have a document to review. Please click the link below to access the document:</p>
        <p style=""text-align: center;"">
            <a href=""{approvalLink}"" class=""button"">Review Document</a>
        </p>
        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #666;"">{approvalLink}</p>
        <div class=""otp-box"">
            <p style=""margin: 0 0 10px 0;""><strong>Your OTP Code:</strong></p>
            <div class=""otp-code"">{otp}</div>
            <p style=""margin: 10px 0 0 0; font-size: 12px; color: #666;"">You will need this OTP to access the document.</p>
        </div>
        <p style=""margin-top: 30px; font-size: 12px; color: #999;"">This link will expire in 24 hours.</p>
    </div>
</body>
</html>";

            await _emailService.SendAsync(
                workflow.ExternalReviewerEmail,
                "Document for external approval",
                body,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the email error and continue; the workflow is already marked as PendingExternalReview
            Console.WriteLine($"[Email Error] Failed to send external review email: {ex.Message}");
        }
    }

    private static string GenerateOtp()
    {
        var random = RandomNumberGenerator.GetInt32(100000, 999999);
        return random.ToString();
    }

    private static string Hash(string value)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }
}


