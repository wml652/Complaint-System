using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize(Roles = "Admin,Staff")]
public class InternalChatController : Controller
{
    private readonly IConversationService _conversationService;

    public InternalChatController(IConversationService conversationService)
    {
        _conversationService = conversationService;
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
}