using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Application.DTOs;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize]
public class ChatWorkspaceController : Controller
{
    private readonly IComplaintService _complaintService;
    private readonly IMessageService _messageService;
    private readonly IConversationService _conversationService;

    public ChatWorkspaceController(
        IComplaintService complaintService,
        IMessageService messageService,
        IConversationService conversationService)
    {
        _complaintService = complaintService;
        _messageService = messageService;
        _conversationService = conversationService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // Students tab ke liye — complaint-based chats
    [HttpGet]
    [HttpGet]
    public async Task<IActionResult> GetStudentChats()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        IEnumerable<ComplaintDto> complaints;

        if (User.IsInRole("Student"))
        {
            complaints = await _complaintService.GetByStudentAsync(userId);
        }
        else if (User.IsInRole("Admin") || User.HasClaim("Permission", "Complaints.ViewAll"))
        {
            complaints = await _complaintService.GetAllAsync();
        }
        else
        {
            complaints = await _complaintService.GetAssignedComplaintsAsync(userId);
        }

        var result = new List<object>();
        foreach (var complaint in complaints)
        {
            var messages = (await _messageService.GetConversationAsync(complaint.Id)).ToList();
            var lastMessage = messages.LastOrDefault();
            var unreadCount = messages.Count(m => m.SenderId != userId && m.ReadAt == null);

            result.Add(new
            {
                complaintId = complaint.Id,
                studentName = User.IsInRole("Student") ? "Support Team" : complaint.StudentName,
                studentId = complaint.StudentId,
                isSupportTeamView = User.IsInRole("Student"),   // naya flag
                title = complaint.Title,
                lastMessagePreview = lastMessage?.Content,
                lastMessageAt = lastMessage?.SentAt,
                unreadCount
            });
        }

        return Json(result.OrderByDescending(r => ((dynamic)r).lastMessageAt));
    }

    // Team tab ke liye — internal conversations
    [HttpGet]
    public async Task<IActionResult> GetTeamChats()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var conversations = await _conversationService.GetConversationsForUserAsync(userId);
        return Json(conversations);
    }
}