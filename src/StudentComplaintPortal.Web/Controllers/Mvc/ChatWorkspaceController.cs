using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Entities;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize]
public class ChatWorkspaceController : Controller
{
    private readonly IComplaintService _complaintService;
    private readonly IMessageService _messageService;
    private readonly IConversationService _conversationService;
    private readonly UserManager<AppUser> _userManager;

    public ChatWorkspaceController(
        IComplaintService complaintService,
        IMessageService messageService,
        IConversationService conversationService,
        UserManager<AppUser> userManager)
    {
        _complaintService = complaintService;
        _messageService = messageService;
        _conversationService = conversationService;
        _userManager = userManager;
    }

   
    [HttpGet]
    public IActionResult Index(int? complaintId = null)
    {
        ViewBag.PreselectedComplaintId = complaintId;
        return View();
    }

    // Students tab ke liye — complaint-based chats
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
                status = complaint.Status,
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
    //4th panel i.e info
    [HttpGet]
    public async Task<IActionResult> GetComplaintDetails(int complaintId)
    {
        var complaint = await _complaintService.GetByIdAsync(complaintId);
        if (complaint == null) return NotFound();

        // FIX 4: Fetch comprehensive student details dynamically
        var student = await _userManager.FindByIdAsync(complaint.StudentId);

        // Build dynamic student info object
        var studentInfo = new Dictionary<string, string>();
        if (student != null)
        {
            studentInfo["Name"] = student.FullName ?? "N/A";
            studentInfo["Email"] = student.Email ?? "N/A";

            // Add phone number if available
            if (!string.IsNullOrEmpty(student.PhoneNumber))
            {
                studentInfo["Phone"] = student.PhoneNumber;
            }

            // Add account created date
            studentInfo["Member Since"] = student.CreatedAt.ToString("MMM dd, yyyy");
        }

        return Json(new
        {
            title = complaint.Title,
            status = complaint.Status,
            description = complaint.Description,
            category = complaint.Category,
            createdAt = complaint.CreatedAt,
            updatedAt = complaint.UpdatedAt,
            studentInfo = studentInfo  // FIX 4: Dynamic student information
        });
    }
}