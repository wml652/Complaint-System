using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
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

    public ChatHub(
        IMessageService messageService,
        IUnitOfWork unitOfWork,
        PresenceTracker presenceTracker,
        UserManager<AppUser> userManager,
        IConversationService conversationService)
    {
        _messageService = messageService;
        _unitOfWork = unitOfWork;
        _presenceTracker = presenceTracker;
        _userManager = userManager;
        _conversationService = conversationService;
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

    public async Task NotifyNewMessage(int complaintId, MessageDto message)
    {
        // ADD THIS LINE:
        Console.WriteLine($"[SIGNALR HUB] Broadcasting message to complaint-{complaintId}");

        await Clients.OthersInGroup($"complaint-{complaintId}").SendAsync("ReceiveMessage", message);
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

        // Send message via service (which handles notifications)
        var messageDto = await _messageService.SendMessageAsync(complaintId, userId, content);

        // Broadcast to all users in the complaint group
        await Clients.Group($"complaint-{complaintId}").SendAsync("ReceiveMessage", messageDto);
    }
}
