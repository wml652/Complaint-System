using StudentComplaintPortal.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentComplaintPortal.Application.Services
{
    // Interface for real-time push (implemented by SignalR integration)
    public interface INotificationPushService
    {
        Task PushNotificationAsync(string userId, NotificationDto notification);
    }
}
