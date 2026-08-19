using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Web.Services;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationService _conversationService;
    private readonly PresenceTracker _presenceTracker;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMessageReadTrackingService _readTrackingService;
    private readonly IMessageQuotaService _quotaService;

    public ChatHub(
        IMessageService messageService,
        IUnitOfWork unitOfWork,
        PresenceTracker presenceTracker,
        UserManager<AppUser> userManager,
        IConversationService conversationService,
        IMessageReadTrackingService readTrackingService,
        IMessageQuotaService quotaService)
    {
        _messageService = messageService;
        _unitOfWork = unitOfWork;
        _presenceTracker = presenceTracker;
        _userManager = userManager;
        _conversationService = conversationService;
        _readTrackingService = readTrackingService;
        _quotaService = quotaService;
    }

    public async Task JoinConversationGroup(int conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
    }

    public async Task SendInternalMessage(int conversationId, string content)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Unauthorized: User not authenticated.");
        }

        var messageDto = await _conversationService.SendMessageAsync(conversationId, userId, content);

        await Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveInternalMessage", messageDto);
    }
    public async Task JoinQueryGroup(int conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
    }

    public async Task SendQueryMessage(int conversationId, string content)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Unauthorized: User not authenticated.");
        }

        var messageDto = await _conversationService.SendMessageAsync(conversationId, userId, content);

        // Agar Staff/Admin reply-kar-raha-hai, use is-conversation-ka-participant-bana-do (agar pehle-se-nahi-hai)
        if (userRole == "Staff" || userRole == "Admin")
        {
            await _conversationService.EnsureParticipantAsync(conversationId, userId);
            var alias = await _conversationService.GetOrAssignQueryAliasAsync(userId);

            // Har-viewer-ke-liye alag-alag-naam-bhejna-hoga (kuch-ko-alias, kuch-ko-real-naam)
            // isliye pehle sabko group-mein Alias-wali-DTO bhejte-hain (jo-Student-ko-turant-mil-jaye)
            var aliasedDto = new StudentComplaintPortal.Application.DTOs.InternalMessageDto
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

            await Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", aliasedDto);

            // Sender (Admin/Staff) ko khud apna-asal-naam-hi-dikhna-chahiye, isliye use alag-se real-naam-wali-DTO bhejo
            await Clients.Caller.SendAsync("ReceiveQueryMessage", messageDto);
        }
        else
        {
            // Student ka apna-bheja-message — sabko-uska-asal-naam-hi-dikhna-chahiye (Student khud-anonymous-nahi-hai)
            await Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveQueryMessage", messageDto);
        }
    }

    public async Task MarkInternalMessagesAsRead(int conversationId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Unauthorized: User not authenticated.");
        }

        await _conversationService.MarkAllAsReadAsync(conversationId, userId);
        await Clients.Group($"conversation-{conversationId}").SendAsync("InternalMessagesRead", conversationId, userId, DateTime.UtcNow);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            bool justCameOnline = _presenceTracker.UserConnected(userId, Context.ConnectionId);
            if (justCameOnline)
            {
                // tells user is online
                await Clients.Others.SendAsync("UserOnline", userId);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            bool wentOffline = _presenceTracker.UserDisconnected(userId, Context.ConnectionId);
            if (wentOffline)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.LastSeenAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                await Clients.Others.SendAsync("UserOffline", userId, DateTime.UtcNow);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
    public async Task<List<string>> GetOnlineUserIds()//to show all online users
    {
        return _presenceTracker.GetOnlineUserIds();
    }

    // Chat khulte waqt kisi specific user ka current online/last-seen status batata hai
    public async Task<object> GetUserPresence(string userId)
    {
        bool isOnline = _presenceTracker.IsOnline(userId);
        DateTime? lastSeen = null;

        if (!isOnline)
        {
            var user = await _userManager.FindByIdAsync(userId);
            lastSeen = user?.LastSeenAt;
        }

        return new { userId, isOnline, lastSeenAt = lastSeen };
    }

    // Jab receiver actually chat kholay aur dekhay (seen)
    public async Task MarkAsRead(int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Unauthorized: User not authenticated.");
        }

        await _messageService.MarkAllAsReadAsync(complaintId, userId);

        // tells that messages of this complaint has been "seen"
        await Clients.Group($"complaint-{complaintId}").SendAsync("MessagesRead", complaintId, userId, DateTime.UtcNow);
    }

    private string GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new HubException("Unauthorized");

    public async Task JoinComplaintGroup(int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
        {
            throw new HubException("Unauthorized: User information not found.");
        }

        // Verify user has access to this complaint
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new HubException($"Complaint with ID {complaintId} not found.");
        }

        // Students can only join their own complaints, admins can join any
        if (userRole == "Student" && complaint.StudentId != userId)
        {
            throw new HubException("Forbidden: You can only join your own complaint groups.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"complaint-{complaintId}");
        await Clients.Caller.SendAsync("JoinedGroup", complaintId);
    }

    public async Task SendMessage(int complaintId, string content)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Unauthorized: User not authenticated.");
        }

        // Verify user has access to this complaint
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new HubException($"Complaint with ID {complaintId} not found.");
        }

        if (userRole == "Student" && complaint.StudentId != userId)
        {
            throw new HubException("Forbidden: You can only send messages to your own complaints.");
        }

        // Check quota for students only
        if (userRole == "Student")
        {
            var canSend = await _quotaService.CanSendMessageAsync(complaintId, userId);

            if (!canSend)
            {
                await Clients.Caller.SendAsync("QuotaExceeded", new
                {
                    message = "You've reached your message limit (10 messages). Please wait for a staff response to continue.",
                    remaining = 0,
                    maxMessages = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE
                });
                return;
            }
        }

        // Send message via service (which handles notifications)
        StudentComplaintPortal.Application.DTOs.MessageDto messageDto;
        try
        {
            messageDto = await _messageService.SendMessageAsync(complaintId, userId, content);
        }
        catch (StudentComplaintPortal.Application.Exceptions.ComplaintClosedException ex)
        {
            throw new HubException(ex.Message);
        }

        // Handle quota updates
        if (userRole == "Student")
        {
            await _quotaService.DecrementQuotaAsync(complaintId, userId);

            var remaining = await _quotaService.GetRemainingMessagesAsync(complaintId, userId);

            // Notify sender of remaining quota
            await Clients.Caller.SendAsync("QuotaUpdated", new
            {
                remaining = remaining,
                maxMessages = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE,
                canSendMore = remaining > 0
            });
        }
        else if (userRole == "Staff" || userRole == "Admin")
        {
            // Reset quota for all students in this complaint
            await _quotaService.ResetQuotaForComplaintAsync(complaintId);

            // Notify all students in the conversation
            await Clients.Group($"complaint-{complaintId}")
                .SendAsync("QuotaReset", new
                {
                    remaining = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE,
                    maxMessages = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE,
                    message = "Staff has responded. You can now send more messages."
                });
        }

        // Broadcast to all users in the complaint group
        await Clients.Group($"complaint-{complaintId}").SendAsync("ReceiveMessage", messageDto);
    }

    // Mark individual message as read
    public async Task MarkMessageAsRead(int messageId, int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("User not authenticated");
        }

        try
        {
            await _readTrackingService.MarkMessageAsReadAsync(messageId, userId);

            var user = await _userManager.FindByIdAsync(userId);

            // Notify all participants in this complaint
            await Clients.Group($"complaint-{complaintId}")
                .SendAsync("MessageRead", new
                {
                    messageId,
                    readAt = DateTime.UtcNow,
                    readByUserId = userId,
                    readByUserName = user?.FullName
                });
        }
        catch (Exception ex)
        {
            throw new HubException($"Failed to mark message as read: {ex.Message}");
        }
    }

    // Mark multiple messages as read
    public async Task MarkMultipleMessagesAsRead(List<int> messageIds, int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("User not authenticated");
        }

        try
        {
            await _readTrackingService.MarkMultipleMessagesAsReadAsync(messageIds, userId);

            // Notify all participants
            await Clients.Group($"complaint-{complaintId}")
                .SendAsync("MultipleMessagesRead", new
                {
                    messageIds,
                    readAt = DateTime.UtcNow,
                    readByUserId = userId
                });
        }
        catch (Exception ex)
        {
            throw new HubException($"Failed to mark messages as read: {ex.Message}");
        }
    }

    #region Typing Indicators

    public async Task UserStartedTyping(int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _userManager.FindByIdAsync(userId);
        var userName = user?.FullName ?? "User";

        await Clients.OthersInGroup($"complaint-{complaintId}")
            .SendAsync("UserTyping", userName, true);
    }

    public async Task UserStoppedTyping(int complaintId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _userManager.FindByIdAsync(userId);
        var userName = user?.FullName ?? "User";

        await Clients.OthersInGroup($"complaint-{complaintId}")
            .SendAsync("UserTyping", userName, false);
    }

    public async Task LeaveComplaintGroup(int complaintId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"complaint-{complaintId}");
    }

    #endregion
}
