using Microsoft.AspNetCore.SignalR;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Web.Hubs;

namespace StudentComplaintPortal.Web.Services;

public class SignalRNotificationPushService : INotificationPushService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRNotificationPushService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushNotificationAsync(string userId, NotificationDto notification)
    {
        // Send to user's personal notification group
        await _hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", notification);
    }
}
