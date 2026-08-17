using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Web.Hubs;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/v1/complaints")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IComplaintService _complaintService;
    private readonly IHubContext<ChatHub> _hubContext;

    public AttachmentsController(
        IAttachmentService attachmentService,
        IComplaintService complaintService,
        IHubContext<ChatHub> hubContext)
    {
        _attachmentService = attachmentService;
        _complaintService = complaintService;
        _hubContext = hubContext;
    }

    [HttpPost("{complaintId}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(
        int complaintId,
        [FromForm] AttachmentUploadRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Parse file type
        if (!Enum.TryParse<FileType>(request.FileType, true, out var parsedFileType))
        {
            return BadRequest(new { message = $"Invalid file type: {request.FileType}. Allowed values: Photo, Video, VoiceNote" });
        }

        // Verify complaint exists and user has access
        var complaint = await _complaintService.GetByIdAsync(complaintId);
        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {complaintId} not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Students can only upload to their own complaints
        if (userRole == "Student" && complaint.StudentId != userId)
        {
            return Forbid();
        }

        // Create message with attachment
        using var fileStream = request.File.OpenReadStream();
        MessageDto messageDto;
        try
        {
            messageDto = await _attachmentService.CreateMessageWithAttachmentAsync(
                complaintId, userId, fileStream, request.File.FileName, request.File.ContentType, parsedFileType, request.Content);
        }
        catch (StudentComplaintPortal.Application.Exceptions.ComplaintClosedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Push via SignalR to all users in the complaint group
        await _hubContext.Clients.Group($"complaint-{complaintId}").SendAsync("ReceiveMessage", messageDto);

        return CreatedAtAction("GetMessages", "Messages", new { id = complaintId }, messageDto);
    }

    [HttpPost("{complaintId}/voice-message")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UploadVoiceMessage(int complaintId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Double-check: enforce role-based access (fail-safe)
        if (userRole != "Admin" && userRole != "Staff")
        {
            return Forbid();
        }

        // Verify complaint exists
        var complaint = await _complaintService.GetByIdAsync(complaintId);
        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {complaintId} not found" });

        try
        {
            using var memoryStream = new MemoryStream();
            await Request.Body.CopyToAsync(memoryStream);

            if (memoryStream.Length == 0)
            {
                return BadRequest(new { message = "Audio stream is empty" });
            }

            // Reset position so the service can read it from the beginning
            memoryStream.Position = 0;

            var messageDto = await _attachmentService.CreateMessageWithAttachmentAsync(
                complaintId,
                userId,
                memoryStream,
                $"voice_{DateTime.UtcNow:yyyyMMdd_HHmmss}.webm",
                "audio/webm",
                FileType.VoiceNote,
                null
            );

            if (messageDto != null)
            {
                // Broadcast the voice note via SignalR
                await _hubContext.Clients.Group($"complaint-{complaintId}").SendAsync("ReceiveMessage", messageDto);
                return CreatedAtAction("GetMessages", "Messages", new { id = complaintId }, messageDto);
            }

            return BadRequest(new { message = "Failed to create voice message" });
        }
        catch (StudentComplaintPortal.Application.Exceptions.ComplaintClosedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error uploading voice message", error = ex.Message });
        }
    }
}
