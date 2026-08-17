using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public interface INotificationService
{
    Task<NotificationDto> NotifyAsync(string userId, string message, NotificationType type);
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId);
    Task<NotificationDto> MarkAsReadAsync(int notificationId, string userId);
}
