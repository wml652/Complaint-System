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
    Task<InternalMessageDto> CreateMessageWithAttachmentAsync(int conversationId, string senderId, Stream fileStream, string fileName, string contentType, StudentComplaintPortal.Domain.Enums.FileType fileType, string? content = null);
    Task<string> GetOrAssignQueryAliasAsync(string staffUserId);
    Task<int> GetOrCreateQueryConversationAsync(string studentId);
    Task<CursorResult<ConversationDto>> GetQueryConversationsPagedAsync(string viewerId, string? cursor, int pageSize = 20, bool moveForward = true);
    Task EnsureParticipantAsync(int conversationId, string userId);
    Task<CursorResult<InternalMessageDto>> GetQueryMessagesPagedAsync(int conversationId, bool viewerCanSeeRealNames, string? cursor, int pageSize = 20, bool moveForward = true);
    Task<InternalMessageDto> EditInternalMessageAsync(int messageId, string userId, string newContent);
    Task<bool> DeleteInternalMessageAsync(int messageId, string userId, bool isAdmin);
}
