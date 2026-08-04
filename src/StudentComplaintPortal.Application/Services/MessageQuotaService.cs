using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Application.Services;

public interface IMessageQuotaService
{
    Task<int> GetRemainingMessagesAsync(int complaintId, string studentId);
    Task<bool> CanSendMessageAsync(int complaintId, string userId);
    Task DecrementQuotaAsync(int complaintId, string studentId);
    Task ResetQuotaForComplaintAsync(int complaintId);
    Task InitializeQuotaAsync(int complaintId, string studentId);
}

public class MessageQuotaService : IMessageQuotaService
{
    private readonly AppDbContext _context;

    public MessageQuotaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetRemainingMessagesAsync(int complaintId, string studentId)
    {
        var quota = await _context.MessageQuotas
            .FirstOrDefaultAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);

        if (quota == null)
        {
            await InitializeQuotaAsync(complaintId, studentId);
            return MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE;
        }

        return quota.MessagesRemaining;
    }

    public async Task<bool> CanSendMessageAsync(int complaintId, string userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return false;

        // Staff and Admin have unlimited messages
        if (user.Role == UserRole.Staff || user.Role == UserRole.Admin)
            return true;

        var remaining = await GetRemainingMessagesAsync(complaintId, userId);
        return remaining > 0;
    }

    public async Task DecrementQuotaAsync(int complaintId, string studentId)
    {
        var quota = await _context.MessageQuotas
            .FirstOrDefaultAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);

        if (quota == null)
        {
            await InitializeQuotaAsync(complaintId, studentId);
            quota = await _context.MessageQuotas
                .FirstAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);
        }

        if (quota.MessagesRemaining > 0)
        {
            quota.MessagesRemaining--;
            quota.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ResetQuotaForComplaintAsync(int complaintId)
    {
        var quotas = await _context.MessageQuotas
            .Where(q => q.ComplaintId == complaintId)
            .ToListAsync();

        foreach (var quota in quotas)
        {
            quota.MessagesRemaining = MessageQuota.MAX_MESSAGES_PER_STAFF_RESPONSE;
            quota.LastStaffMessageAt = DateTime.UtcNow;
            quota.UpdatedAt = DateTime.UtcNow;
        }

        if (quotas.Any())
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task InitializeQuotaAsync(int complaintId, string studentId)
    {
        var exists = await _context.MessageQuotas
            .AnyAsync(q => q.ComplaintId == complaintId && q.StudentId == studentId);

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

            _context.MessageQuotas.Add(quota);
            await _context.SaveChangesAsync();
        }
    }
}
