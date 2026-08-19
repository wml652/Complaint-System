using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Data.Repositories;

public interface IConversationRepository : IGenericRepository<Conversation>
{
    Task<List<int>> GetConversationIdsForUserAsync(string userId);
    Task<List<Conversation>> GetConversationsWithParticipantsAsync(List<int> conversationIds);
    Task<Conversation?> FindDirectConversationAsync(string userId1, string userId2);

    Task<List<ConversationParticipant>> GetParticipantsAsync(int conversationId);
    Task<ConversationParticipant?> GetParticipantAsync(int conversationId, string userId);
    Task AddParticipantsAsync(IEnumerable<ConversationParticipant> participants);

    Task<InternalMessage?> GetLastMessageAsync(int conversationId);
    Task<List<InternalMessage>> GetMessagesWithSenderAsync(int conversationId);
    Task<int> GetUnreadCountAsync(int conversationId, string userId, DateTime? since);
    Task<InternalMessage> AddMessageAsync(InternalMessage message);
    Task<InternalMessage?> GetMessageByIdAsync(int messageId);
    void UpdateMessage(InternalMessage message);
    Task<InternalAttachment> AddAttachmentAsync(InternalAttachment attachment);
    Task<List<AppUser>> GetStaffAndAdminContactsAsync(string excludeUserId);
    Task<AppUser?> GetUserAsync(string userId);
    Task<Conversation?> GetPinnedGroupForUserAsync(string userId);
    Task<List<Conversation>> GetConversationsPagedForUserAsync(string userId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
    Task UpdateUserQueryAliasAsync(string userId, string alias);
    Task<Conversation?> FindQueryConversationForStudentAsync(string studentId);
    Task<List<Conversation>> GetQueryConversationsPagedAsync(DateTime? cursorTimestamp, int pageSize, bool moveForward = true);
}