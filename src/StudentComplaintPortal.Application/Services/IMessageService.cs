using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface IMessageService
{
    Task<MessageDto> SendMessageAsync(int complaintId, string senderId, string? content, List<int>? attachmentIds = null);
    Task<IEnumerable<MessageDto>> GetConversationAsync(int complaintId);
    Task<MessageDto> GetMessageByIdAsync(int messageId);

    Task MarkAllAsReadAsync(int complaintId, string readerUserId);
    Task<MessageDto?> EditMessageAsync(int messageId, string userId, string newContent, bool isAdmin);
    Task<bool> DeleteMessageAsync(int messageId, string userId, bool isAdmin);
}
