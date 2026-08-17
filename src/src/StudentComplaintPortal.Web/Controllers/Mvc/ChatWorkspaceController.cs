using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Web.Hubs;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Controllers.Mvc;

[Authorize]
public class ChatWorkspaceController : Controller
{
    private readonly IComplaintService _complaintService;
    private readonly IMessageService _messageService;
    private readonly IConversationService _conversationService;
    private readonly ICategoryService _categoryService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatWorkspaceController(
        IComplaintService complaintService,
        IMessageService messageService,
        IConversationService conversationService,
        ICategoryService categoryService,
        UserManager<AppUser> userManager,
        IHubContext<ChatHub> hubContext)
    {
        _complaintService = complaintService;
        _messageService = messageService;
        _conversationService = conversationService;
        _categoryService = categoryService;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? complaintId = null, bool openQuery = false)
    {
        ViewBag.PreselectedComplaintId = complaintId;
        ViewBag.OpenQuery = openQuery;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole("Admin") || User.HasClaim("Permission", "Complaints.ViewAll"))
        {
            ViewBag.FilterCategories = await _categoryService.GetAllActiveCategoriesAsync();
        }
        else if (!User.IsInRole("Student") && !string.IsNullOrEmpty(userId))
        {
            ViewBag.FilterCategories = await _categoryService.GetCategoriesForStaffAsync(userId);
        }
        else
        {
            ViewBag.FilterCategories = Enumerable.Empty<CategoryDto>();
        }

        return View();
    }

    // Students tab ke liye — complaint-based chats
    [HttpGet]
    public async Task<IActionResult> GetStudentChats(string? cursor = null, int pageSize = 20, bool moveForward = true, int? categoryId = null, ComplaintStatus? status = null, bool unreadOnly = false, DateTime? startDate = null, DateTime? endDate = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        CursorResult<ComplaintDto> pagedComplaints;

        if (User.IsInRole("Student"))
        {
            pagedComplaints = await _complaintService.GetByStudentPagedAsync(userId, cursor, pageSize, moveForward);
        }
        else if (User.IsInRole("Admin") || User.HasClaim("Permission", "Complaints.ViewAll"))
        {
            pagedComplaints = await _complaintService.GetFilteredPagedAsync(categoryId, status, unreadOnly, userId, null, startDate, endDate, cursor, pageSize, moveForward);
        }
        else
        {
            pagedComplaints = await _complaintService.GetFilteredPagedAsync(categoryId, status, unreadOnly, userId, userId, startDate, endDate, cursor, pageSize, moveForward);
        }

        var result = new List<object>();

        // Sirf pehli-page par (cursor null hone par) Student ke liye pinned "Support" query-row add karo
        if (User.IsInRole("Student") && string.IsNullOrEmpty(cursor))
        {
            var queryConversationId = await _conversationService.GetOrCreateQueryConversationAsync(userId);
            var queryMessages = await _conversationService.GetQueryMessagesPagedAsync(queryConversationId, false, null, 1, false);
            var lastQueryMessage = queryMessages.Items.LastOrDefault();
            var queryUnreadCount = queryMessages.Items.Count(m => m.SenderId != userId && m.ReadAt == null);

            result.Add(new
            {
                complaintId = (int?)null,
                queryConversationId = queryConversationId,
                isQueryRow = true,
                studentName = "Support",
                title = "Support",
                status = (string?)null,
                lastMessagePreview = lastQueryMessage?.Content,
                lastMessageAt = lastQueryMessage?.SentAt,
                unreadCount = queryUnreadCount
            });
        }

        foreach (var complaint in pagedComplaints.Items)
        {
            var messages = (await _messageService.GetConversationAsync(complaint.Id)).ToList();
            var lastMessage = messages.LastOrDefault();
            var unreadCount = messages.Count(m => m.SenderId != userId && m.ReadAt == null);

            result.Add(new
            {
                complaintId = (int?)complaint.Id,
                queryConversationId = (int?)null,
                isQueryRow = false,
                studentName = User.IsInRole("Student") ? "Support Team" : complaint.StudentName,
                studentId = complaint.StudentId,
                isSupportTeamView = User.IsInRole("Student"),
                title = complaint.Title,
                status = complaint.Status,
                lastMessagePreview = lastMessage?.Content,
                lastMessageAt = lastMessage?.SentAt,
                unreadCount
            });
        }

        return Json(new { items = result, nextCursor = pagedComplaints.NextCursor, hasMore = pagedComplaints.HasMore });
    }

    // Team tab ke liye — internal conversations
    [HttpGet]
    public async Task<IActionResult> GetTeamChats(string? cursor = null, int pageSize = 20, bool moveForward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var pagedConversations = await _conversationService.GetConversationsPagedForUserAsync(userId, cursor, pageSize, moveForward);

        return Json(new { items = pagedConversations.Items, nextCursor = pagedConversations.NextCursor, hasMore = pagedConversations.HasMore });
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
    //for student
    [HttpGet]
    public async Task<IActionResult> GetOrCreateMyQueryConversation()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var conversationId = await _conversationService.GetOrCreateQueryConversationAsync(userId);
        return Json(new { conversationId });
    }
    //for staff/admin
    [HttpGet]
    public async Task<IActionResult> GetQueryChats(string? cursor = null, int pageSize = 20, bool moveForward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var pagedQueries = await _conversationService.GetQueryConversationsPagedAsync(userId, cursor, pageSize, moveForward);
        return Json(new { items = pagedQueries.Items, nextCursor = pagedQueries.NextCursor, hasMore = pagedQueries.HasMore });
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadQueryAttachment(int conversationId, [FromForm] AttachmentUploadRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<FileType>(request.FileType, true, out var parsedFileType))
        {
            return BadRequest(new { message = $"Invalid file type: {request.FileType}" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        // Student sirf apni-khud-ki-Query-Conversation mein-hi-attachment-bhej-sake
        if (User.IsInRole("Student"))
        {
            var myConversationId = await _conversationService.GetOrCreateQueryConversationAsync(userId);
            if (myConversationId != conversationId)
            {
                return Forbid();
            }
        }
        else if (User.IsInRole("Staff") || User.IsInRole("Admin"))
        {
            await _conversationService.EnsureParticipantAsync(conversationId, userId);
        }

        using var fileStream = request.File.OpenReadStream();
        var messageDto = await _conversationService.CreateMessageWithAttachmentAsync(
            conversationId, userId, fileStream, request.File.FileName, request.File.ContentType, parsedFileType, request.Content);

        // Alias-vs-real-naam ka logic yahan bhi lagana zaroori hai (text-messages jaisa)
        bool canSeeRealNames = !User.IsInRole("Student") &&
            (User.IsInRole("Admin") || User.HasClaim("Permission", "Queries.ViewRealNames"));

        var sender = await _userManager.FindByIdAsync(userId);
        if (sender != null && (sender.Role == UserRole.Staff || sender.Role == UserRole.Admin) && !canSeeRealNames)
        {
            var alias = await _conversationService.GetOrAssignQueryAliasAsync(userId);
            var aliasedDto = new InternalMessageDto
            {
                Id = messageDto.Id,
                ConversationId = messageDto.ConversationId,
                SenderId = messageDto.SenderId,
                SenderName = alias,
                Content = messageDto.Content,
                SentAt = messageDto.SentAt,
                ReadAt = messageDto.ReadAt,
                Attachments = messageDto.Attachments
            };
            await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", aliasedDto);
            return Ok(messageDto);
        }

        await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", messageDto);
        return Ok(messageDto);
    }

    // Team-chat aur Query-chat dono ke liye voice-message upload (InternalMessage-based, generic)
    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UploadInternalVoiceMessage(int conversationId, [FromQuery] string chatType)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role)!;

        // Double-check: enforce role-based access (fail-safe, AttachmentsController.UploadVoiceMessage jaisa)
        if (userRole != "Admin" && userRole != "Staff")
        {
            return Forbid();
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await Request.Body.CopyToAsync(memoryStream);

            if (memoryStream.Length == 0)
            {
                return BadRequest(new { message = "Audio stream is empty" });
            }

            memoryStream.Position = 0;

            var messageDto = await _conversationService.CreateMessageWithAttachmentAsync(
                conversationId,
                userId,
                memoryStream,
                $"voice_{DateTime.UtcNow:yyyyMMdd_HHmmss}.webm",
                "audio/webm",
                FileType.VoiceNote,
                null
            );

            if (chatType == "query")
            {
                // Query-chat ke liye alias-vs-real-naam ka logic (UploadQueryAttachment jaisa)
                bool canSeeRealNames = User.IsInRole("Admin") || User.HasClaim("Permission", "Queries.ViewRealNames");

                if (!canSeeRealNames)
                {
                    var alias = await _conversationService.GetOrAssignQueryAliasAsync(userId);
                    var aliasedDto = new InternalMessageDto
                    {
                        Id = messageDto.Id,
                        ConversationId = messageDto.ConversationId,
                        SenderId = messageDto.SenderId,
                        SenderName = alias,
                        Content = messageDto.Content,
                        SentAt = messageDto.SentAt,
                        ReadAt = messageDto.ReadAt,
                        Attachments = messageDto.Attachments
                    };
                    await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", aliasedDto);
                    return Ok(messageDto);
                }

                await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", messageDto);
            }
            else
            {
                // Team-chat — koi alias-logic nahi, seedha real-naam broadcast
                await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveInternalMessage", messageDto);
            }

            return Ok(messageDto);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error uploading voice message" });
        }
    } 


    [HttpGet]
    public async Task<IActionResult> GetQueryMessagesPaged(int conversationId, string? cursor = null, int pageSize = 20, bool moveForward = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        if (User.IsInRole("Student"))
        {
            var myConversationId = await _conversationService.GetOrCreateQueryConversationAsync(userId);
            if (myConversationId != conversationId)
            {
                return Forbid();
            }
        }

        bool canSeeRealNames = !User.IsInRole("Student") &&
            (User.IsInRole("Admin") || User.HasClaim("Permission", "Queries.ViewRealNames"));

        var result = await _conversationService.GetQueryMessagesPagedAsync(conversationId, canSeeRealNames, cursor, pageSize, moveForward);
        return Json(result);
    }

    // Team-chat aur Query-chat dono ke liye — message edit (InternalMessage-based)
    [HttpPut]
    public async Task<IActionResult> EditInternalMessage(int conversationId, int messageId, [FromBody] EditMessageRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        try
        {
            var updatedMessage = await _conversationService.EditInternalMessageAsync(messageId, userId, request.Content);

            await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("MessageEdited", updatedMessage);

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

    // Team-chat aur Query-chat dono ke liye — message delete (InternalMessage-based)
    [HttpDelete]
    public async Task<IActionResult> DeleteInternalMessage(int conversationId, int messageId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        bool isAdmin = User.IsInRole("Admin");

        try
        {
            var success = await _conversationService.DeleteInternalMessageAsync(messageId, userId, isAdmin);

            if (success)
            {
                await _hubContext.Clients.Group($"conversation-{conversationId}").SendAsync("MessageDeleted", messageId);
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