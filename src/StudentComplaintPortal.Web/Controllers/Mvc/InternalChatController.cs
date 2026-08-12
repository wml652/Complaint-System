using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Web.Hubs;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize(Roles = "Admin,Staff")]
public class InternalChatController : Controller
{
    private readonly IConversationService _conversationService;
    private readonly IHubContext<ChatHub> _hubContext;

    public InternalChatController(IConversationService conversationService, IHubContext<ChatHub> hubContext)
    {
        _conversationService = conversationService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var conversations = await _conversationService.GetConversationsForUserAsync(userId);
        return View(conversations);
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(int conversationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var messages = await _conversationService.GetMessagesAsync(conversationId);
        return Json(messages);
    }

    [HttpGet]
    public async Task<IActionResult> GetMessagesPaged(int conversationId, string? cursor = null, int pageSize = 20, bool forward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var result = await _conversationService.GetMessagesPagedAsync(conversationId, cursor, pageSize, forward);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers(int conversationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var members = await _conversationService.GetParticipantsAsync(conversationId);
        return Json(members);
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var contacts = await _conversationService.GetContactsAsync(userId);
        return Json(contacts);
    }

    [HttpPost]
    public async Task<IActionResult> StartDirectConversation(string otherUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var conversationId = await _conversationService.GetOrCreateDirectConversationAsync(userId, otherUserId);
        return Json(new { conversationId });
    }
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(int conversationId, [FromForm] AttachmentUploadRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<StudentComplaintPortal.Domain.Enums.FileType>(request.FileType, true, out var parsedFileType))
        {
            return BadRequest(new { message = $"Invalid file type: {request.FileType}" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        using var fileStream = request.File.OpenReadStream();
        var messageDto = await _conversationService.CreateMessageWithAttachmentAsync(
            conversationId, userId, fileStream, request.File.FileName, request.File.ContentType, parsedFileType, request.Content);

        // Sabko turant, live-batao (jaisa AttachmentsController complaint-wale ke liye karta hai)
        await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveInternalMessage", messageDto);

        return Ok(messageDto);
    }


}