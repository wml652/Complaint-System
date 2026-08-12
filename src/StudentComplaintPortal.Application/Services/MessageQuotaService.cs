using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public class MessageQuotaService : IMessageQuotaService
{
    private readonly IUnitOfWork _unitOfWork;

    public MessageQuotaService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> GetRemainingMessagesAsync(int complaintId, string studentId)
    {
        var quota = await _unitOfWork.MessageQuotas.GetAsync(complaintId, studentId);

        if (quota == null)
        {
            await InitializeQuotaAsync(complaintId, studentId);
            return MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE;
        }

        return quota.MessagesRemaining;
    }

    public async Task<bool> CanSendMessageAsync(int complaintId, string userId)
    {
        var user = await _unitOfWork.Conversations.GetUserAsync(userId);

        if (user == null)
            return false;

        if (user.Role == UserRole.Staff || user.Role == UserRole.Admin)
            return true;

        var remaining = await GetRemainingMessagesAsync(complaintId, userId);
        return remaining > 0;
    }

    public async Task DecrementQuotaAsync(int complaintId, string studentId)
    {
        var quota = await _unitOfWork.MessageQuotas.GetAsync(complaintId, studentId);

        if (quota == null)
        {
            await InitializeQuotaAsync(complaintId, studentId);
            quota = await _unitOfWork.MessageQuotas.GetAsync(complaintId, studentId);
        }

        if (quota != null && quota.MessagesRemaining > 0)
        {
            quota.MessagesRemaining--;
            quota.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ResetQuotaForComplaintAsync(int complaintId)
    {
        var quotas = await _unitOfWork.MessageQuotas.GetAllForComplaintAsync(complaintId);

        foreach (var quota in quotas)
        {
            quota.MessagesRemaining = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE;
            quota.LastStaffMessageAt = DateTime.UtcNow;
            quota.UpdatedAt = DateTime.UtcNow;
        }

        if (quotas.Any())
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task InitializeQuotaAsync(int complaintId, string studentId)
    {
        var exists = await _unitOfWork.MessageQuotas.ExistsAsync(complaintId, studentId);

        if (!exists)
        {
            var quota = new MessageQuota
            {
                ComplaintId = complaintId,
                StudentId = studentId,
                MessagesRemaining = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.MessageQuotas.AddAsync(quota);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}