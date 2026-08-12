using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Application.Exceptions;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPushService? _pushService;

    public NotificationService(IUnitOfWork unitOfWork, INotificationPushService? pushService = null)
    {
        _unitOfWork = unitOfWork;
        _pushService = pushService;
    }

    public async Task<NotificationDto> NotifyAsync(string userId, string message, NotificationType type)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();

        // Push notification in real-time if push service is available
        if (_pushService != null)
        {
            var dto = MapToDto(notification);
            await _pushService.PushNotificationAsync(userId, dto);
        }

        return MapToDto(notification);
    }

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId)
    {
        var notifications = await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId);
        return notifications.OrderByDescending(n => n.CreatedAt).Select(MapToDto);
    }

    public async Task<NotificationDto> MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
        
        if (notification == null)
        {
            throw new NotFoundException($"Notification with ID {notificationId} not found.");
        }

        if (notification.UserId != userId)
        {
            throw new UnauthorizedComplaintAccessException("You can only mark your own notifications as read.");
        }

        notification.IsRead = true;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(notification);
    }

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        };
    }
}
