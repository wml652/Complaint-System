using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentComplaintPortal.Application.Services
{
    public interface IMessageReadTrackingService
    {
        Task MarkMessageAsReadAsync(int messageId, string userId);
        Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, string userId);
        Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId);
    }
}
