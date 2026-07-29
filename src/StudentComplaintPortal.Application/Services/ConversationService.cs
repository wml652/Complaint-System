using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Application.Services;

public class ConversationService : IConversationService
{
    private readonly AppDbContext _dbContext;

    public ConversationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ConversationDto>> GetConversationsForUserAsync(string userId)
    {
        var conversationIds = await _dbContext.ConversationParticipants
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .ToListAsync();

        var conversations = await _dbContext.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Where(c => conversationIds.Contains(c.Id))
            .ToListAsync();

        var result = new List<ConversationDto>();

        foreach (var conv in conversations)
        {
            var lastMessage = await _dbContext.InternalMessages
                .Where(m => m.ConversationId == conv.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            var unreadCount = await _dbContext.InternalMessages
                .CountAsync(m => m.ConversationId == conv.Id && m.SenderId != userId && m.ReadAt == null);

            string? displayName = conv.Name;
            string? otherUserId = null;

            if (conv.Type == ConversationType.Direct)
            {
                var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
                displayName = otherParticipant?.User.FullName;
                otherUserId = otherParticipant?.UserId;
            }

            result.Add(new ConversationDto
            {
                Id = conv.Id,
                Type = conv.Type.ToString(),
                Name = displayName,
                OtherUserId = otherUserId,
                UnreadCount = unreadCount,
                LastMessagePreview = lastMessage?.Content,
                LastMessageAt = lastMessage?.SentAt
            });
        }

        // Pinned "Team" group hamesha sabse upar, baaki naye message ke hisaab se sorted
        return result
            .OrderByDescending(c => c.Type == "Group")
            .ThenByDescending(c => c.LastMessageAt)
            .ToList();
    }

    public async Task<int> GetOrCreateDirectConversationAsync(string userId1, string userId2)
    {
        // find existing direct conversation which has both participants 
        var existing = await _dbContext.Conversations
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => c.Participants.Any(p => p.UserId == userId1))
            .Where(c => c.Participants.Any(p => p.UserId == userId2))
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return existing.Id;
        }

        var newConversation = new Conversation
        {
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Conversations.Add(newConversation);
        await _dbContext.SaveChangesAsync();

        _dbContext.ConversationParticipants.AddRange(
            new ConversationParticipant { ConversationId = newConversation.Id, UserId = userId1 },
            new ConversationParticipant { ConversationId = newConversation.Id, UserId = userId2 }
        );
        await _dbContext.SaveChangesAsync();

        return newConversation.Id;
    }

    public async Task<List<InternalMessageDto>> GetMessagesAsync(int conversationId)
    {
        return await _dbContext.InternalMessages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .Select(m => new InternalMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.FullName,
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt
            })
            .ToListAsync();
    }

    public async Task<InternalMessageDto> SendMessageAsync(int conversationId, string senderId, string content)
    {
        var message = new InternalMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        _dbContext.InternalMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        var sender = await _dbContext.Users.FindAsync(senderId);

        return new InternalMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? string.Empty,
            Content = message.Content,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt
        };
    }

    public async Task MarkAllAsReadAsync(int conversationId, string readerUserId)
    {
        var messages = await _dbContext.InternalMessages
            .Where(m => m.ConversationId == conversationId && m.SenderId != readerUserId && m.ReadAt == null)
            .ToListAsync();

        if (messages.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var message in messages)
            {
                message.ReadAt = now;
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}