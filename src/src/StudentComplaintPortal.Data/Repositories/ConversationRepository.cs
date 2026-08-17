using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;

namespace StudentComplaintPortal.Data.Repositories;

public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<int>> GetConversationIdsForUserAsync(string userId)
    {
        return await _context.ConversationParticipants
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetConversationsWithParticipantsAsync(List<int> conversationIds)
    {
        return await _dbSet
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Where(c => conversationIds.Contains(c.Id))
            .ToListAsync();
    }

    public async Task<Conversation?> FindDirectConversationAsync(string userId1, string userId2)
    {
        return await _dbSet
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => c.Participants.Any(p => p.UserId == userId1))
            .Where(c => c.Participants.Any(p => p.UserId == userId2))
            .FirstOrDefaultAsync();
    }

    public async Task<List<ConversationParticipant>> GetParticipantsAsync(int conversationId)
    {
        return await _context.ConversationParticipants
            .Include(p => p.User)
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync();
    }

    public async Task<ConversationParticipant?> GetParticipantAsync(int conversationId, string userId)
    {
        return await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);
    }

    public async Task AddParticipantsAsync(IEnumerable<ConversationParticipant> participants)
    {
        await _context.ConversationParticipants.AddRangeAsync(participants);
    }

    public async Task<InternalMessage?> GetLastMessageAsync(int conversationId)
    {
        return await _context.InternalMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<InternalMessage>> GetMessagesWithSenderAsync(int conversationId)
    {
        return await _context.InternalMessages
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int conversationId, string userId, DateTime? since)
    {
        return await _context.InternalMessages
            .CountAsync(m => m.ConversationId == conversationId && m.SenderId != userId
                && (since == null || m.SentAt > since));
    }

    public async Task<InternalMessage> AddMessageAsync(InternalMessage message)
    {
        await _context.InternalMessages.AddAsync(message);
        return message;
    }

    public async Task<InternalMessage?> GetMessageByIdAsync(int messageId)
    {
        return await _context.InternalMessages.FirstOrDefaultAsync(m => m.Id == messageId);
    }

    public void UpdateMessage(InternalMessage message)
    {
        _context.InternalMessages.Update(message);
    }
    public async Task<InternalAttachment> AddAttachmentAsync(InternalAttachment attachment)
    {
        await _context.InternalAttachments.AddAsync(attachment);
        return attachment;
    }

    public async Task<List<AppUser>> GetStaffAndAdminContactsAsync(string excludeUserId)
    {
        return await _context.Users
            .Where(u => (u.Role == UserRole.Admin || u.Role == UserRole.Staff)
                        && u.Id != excludeUserId)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<AppUser?> GetUserAsync(string userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<Conversation?> GetPinnedGroupForUserAsync(string userId)
    {
        return await _dbSet
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Where(c => c.Type == ConversationType.Group && c.Name == "Team")
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .FirstOrDefaultAsync();
    }

    public async Task<List<Conversation>> GetConversationsPagedForUserAsync(string userId, DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .Where(c => !(c.Type == ConversationType.Group && c.Name == "Team"))
            .Where(c => c.Type != ConversationType.Direct || c.LastMessageAt != null)
            .AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }
    public async Task UpdateUserQueryAliasAsync(string userId, string alias)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.QueryAlias = alias;
        }
    }
    public async Task<Conversation?> FindQueryConversationForStudentAsync(string studentId)
    {
        return await _dbSet
            .Where(c => c.Type == ConversationType.Query)
            .Where(c => c.Participants.Any(p => p.UserId == studentId))
            .FirstOrDefaultAsync();
    }

    public async Task<List<Conversation>> GetQueryConversationsPagedAsync(DateTime? cursorTimestamp, int pageSize, bool moveForward = true)
    {
        var query = _dbSet
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Where(c => c.Type == ConversationType.Query)
            .AsQueryable();

        if (cursorTimestamp.HasValue)
        {
            query = moveForward
                ? query.Where(c => (c.LastMessageAt ?? c.CreatedAt) < cursorTimestamp.Value)
                : query.Where(c => (c.LastMessageAt ?? c.CreatedAt) > cursorTimestamp.Value);
        }

        return await query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).Take(pageSize + 1).ToListAsync();
    }
}