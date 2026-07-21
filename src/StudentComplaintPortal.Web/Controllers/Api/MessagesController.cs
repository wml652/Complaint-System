using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Api;

[ApiController]
[Route("api/v1/complaints")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IComplaintService _complaintService;

    public MessagesController(IMessageService messageService, IComplaintService complaintService)
    {
        _messageService = messageService;
        _complaintService = complaintService;
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

        var message = await _messageService.SendMessageAsync(id, userId, request.Content);
        return CreatedAtAction(nameof(GetMessages), new { id }, message);
    }
}
