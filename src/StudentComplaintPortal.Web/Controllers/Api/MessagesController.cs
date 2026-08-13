using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Web.Hubs;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/v1/complaints")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IComplaintService _complaintService;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessagesController(IMessageService messageService, IComplaintService complaintService, IHubContext<ChatHub> hubContext)
    {
        _messageService = messageService;
        _complaintService = complaintService;
        _hubContext = hubContext;
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var complaint = await _complaintService.GetByIdAsync(id);
        
        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {id} not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Students can only view messages for their own complaints
        if (userRole == "Student" && complaint.StudentId != userId)
        {
            return Forbid();
        }

        var messages = await _messageService.GetConversationAsync(id);
        return Ok(messages);
    }

    [HttpGet("{id}/messages/paged")]
    public async Task<IActionResult> GetMessagesPaged(int id, string? cursor = null, int pageSize = 20, bool forward = true)
    {
        var complaint = await _complaintService.GetByIdAsync(id);

        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {id} not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        if (userRole == "Student" && complaint.StudentId != userId)
        {
            return Forbid();
        }

        var messages = await _messageService.GetConversationPagedAsync(id, cursor, pageSize, forward);
        return Ok(messages);
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var complaint = await _complaintService.GetByIdAsync(id);

        if (complaint == null)
            return NotFound(new { message = $"Complaint with ID {id} not found" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Students can only send messages to their own complaints
        if (userRole == "Student" && complaint.StudentId != userId)
        {
            return Forbid();
        }

        try
        {
            var message = await _messageService.SendMessageAsync(id, userId, request.Content);
            return CreatedAtAction(nameof(GetMessages), new { id }, message);
        }
        catch (StudentComplaintPortal.Application.Exceptions.ComplaintClosedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{complaintId}/messages/{messageId}")]
    public async Task<IActionResult> EditMessage(int complaintId, int messageId, [FromBody] EditMessageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;
        var isAdmin = userRole == "Admin";

        try
        {
            var updatedMessage = await _messageService.EditMessageAsync(messageId, userId, request.Content, isAdmin);

            if (updatedMessage != null)
            {
                // Broadcast via SignalR
                await _hubContext.Clients.Group($"complaint-{complaintId}").SendAsync("MessageEdited", updatedMessage);
            }

            return Ok(updatedMessage);
        }
        catch (StudentComplaintPortal.Application.Exceptions.NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (StudentComplaintPortal.Application.Exceptions.UnauthorizedComplaintAccessException)
        {
            return Forbid();
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error editing message" });
        }
    }

    [HttpDelete("{complaintId}/messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(int complaintId, int messageId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;
        var isAdmin = userRole == "Admin";

        try
        {
            var success = await _messageService.DeleteMessageAsync(messageId, userId, isAdmin);

            if (success)
            {
                // Broadcast via SignalR
                await _hubContext.Clients.Group($"complaint-{complaintId}").SendAsync("MessageDeleted", messageId);
            }

            return Ok(new { message = "Message deleted successfully" });
        }
        catch (StudentComplaintPortal.Application.Exceptions.NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (StudentComplaintPortal.Application.Exceptions.UnauthorizedComplaintAccessException)
        {
            return Forbid();
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error deleting message" });
        }
    }
}
