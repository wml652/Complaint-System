using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentComplaintPortal.Application.Services
{
    public interface IMessageQuotaService
    {
        Task<int> GetRemainingMessagesAsync(int complaintId, string studentId);
        Task<bool> CanSendMessageAsync(int complaintId, string userId);
        Task DecrementQuotaAsync(int complaintId, string studentId);
        Task ResetQuotaForComplaintAsync(int complaintId);
        Task InitializeQuotaAsync(int complaintId, string studentId);
    }
}
