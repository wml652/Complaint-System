using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Data.Repositories;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUnitOfWork _unitOfWork;

    public ChatHub(IMessageService messageService, IUnitOfWork unitOfWork)
    {
        _messageService = messageService;
        _unitOfWork = unitOfWork;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal notification group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

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
