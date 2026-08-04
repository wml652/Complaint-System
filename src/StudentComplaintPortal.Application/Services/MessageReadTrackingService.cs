using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Data;

namespace StudentComplaintPortal.Application.Services;

public interface IMessageReadTrackingService
{
    Task MarkMessageAsReadAsync(int messageId, string userId);
    Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, string userId);
    Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId);
}

public class MessageReadTrackingService : IMessageReadTrackingService
{
    private readonly AppDbContext _context;

    public MessageReadTrackingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task MarkMessageAsReadAsync(int messageId, string userId)
    {
        var message = await _context.Messages.FindAsync(messageId);

        if (message == null)
            throw new KeyNotFoundException($"Message {messageId} not found");

        // Don't mark own messages as read
        if (message.SenderId == userId)
            return;

        // Don't overwrite existing read receipt
        if (message.ReadAt.HasValue)
            return;

        message.ReadAt = DateTime.UtcNow;
        message.ReadByUserId = userId;

        await _context.SaveChangesAsync();
    }

    public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, string userId)
    {
        var messages = await _context.Messages
            .Where(m => messageIds.Contains(m.Id) && m.SenderId != userId && !m.ReadAt.HasValue)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.ReadAt = DateTime.UtcNow;
            message.ReadByUserId = userId;
        }

        if (messages.Any())
            await _context.SaveChangesAsync();
    }

    public async Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId)
    {
        return await _context.Messages
            .Where(m => m.ComplaintId == complaintId
                     && m.SenderId != userId
                     && !m.ReadAt.HasValue)
            .Select(m => m.Id)
            .ToListAsync();
    }
}
