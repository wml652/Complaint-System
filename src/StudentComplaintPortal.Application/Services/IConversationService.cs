using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.Services;

public interface IConversationService
{
    Task<List<ConversationDto>> GetConversationsForUserAsync(string userId);
    Task<int> GetOrCreateDirectConversationAsync(string userId1, string userId2);
    Task<List<InternalMessageDto>> GetMessagesAsync(int conversationId);
    Task<CursorResult<InternalMessageDto>> GetMessagesPagedAsync(int conversationId, string? cursor, int pageSize = 20, bool moveForward = true);
    Task<InternalMessageDto> SendMessageAsync(int conversationId, string senderId, string content);
    Task MarkAllAsReadAsync(int conversationId, string readerUserId);
    Task<List<ParticipantDto>> GetParticipantsAsync(int conversationId);
    Task<List<ParticipantDto>> GetContactsAsync(string currentUserId);
    Task<CursorResult<ConversationDto>> GetConversationsPagedForUserAsync(string userId, string? cursor, int pageSize = 20, bool moveForward = true);
}