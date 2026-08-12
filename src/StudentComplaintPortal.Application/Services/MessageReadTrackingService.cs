using StudentComplaintPortal.Data.Repositories;

namespace StudentComplaintPortal.Application.Services;

public class MessageReadTrackingService : IMessageReadTrackingService
{
    private readonly IUnitOfWork _unitOfWork;

    public MessageReadTrackingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task MarkMessageAsReadAsync(int messageId, string userId)
    {
        var message = await _unitOfWork.Messages.GetByIdAsync(messageId);

        if (message == null)
            throw new KeyNotFoundException($"Message {messageId} not found");

        if (message.SenderId == userId)
            return;

        if (message.ReadAt.HasValue)
            return;

        message.ReadAt = DateTime.UtcNow;
        message.ReadByUserId = userId;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, string userId)
    {
        var messages = await _unitOfWork.Messages
            .FindAsync(m => messageIds.Contains(m.Id) && m.SenderId != userId && !m.ReadAt.HasValue);

        var messageList = messages.ToList();

        foreach (var message in messageList)
        {
            message.ReadAt = DateTime.UtcNow;
            message.ReadByUserId = userId;
        }

        if (messageList.Any())
            await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<int>> GetUnreadMessageIdsAsync(int complaintId, string userId)
    {
        return await _unitOfWork.Messages.GetUnreadMessageIdsAsync(complaintId, userId);
    }
}