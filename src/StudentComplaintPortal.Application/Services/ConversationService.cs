using Microsoft.EntityFrameworkCore;
using StudentComplaintPortal.Application.DTOs;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;

namespace StudentComplaintPortal.Application.Services;

public class ConversationService : IConversationService
{
    private readonly AppDbContext _dbContext;

    // Har user-pair ke liye ek lock, taake "+" button do dafa jaldi jaldi
    // dabne se 2 log ek sath duplicate conversation na bana sakein.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _directConversationLocks = new();

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

            // Direct (1-to-1) chat jisme abhi tak koi message nahi - list mein mat dikhao
            if (conv.Type == ConversationType.Direct && lastMessage == null)
            {
                continue;
            }

            var myParticipant = conv.Participants.FirstOrDefault(p => p.UserId == userId);
            var myLastReadAt = myParticipant?.LastReadAt;

            var unreadCount = await _dbContext.InternalMessages
                .CountAsync(m => m.ConversationId == conv.Id && m.SenderId != userId
                    && (myLastReadAt == null || m.SentAt > myLastReadAt));

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
        // Dono log chahe kisi bhi order mein call karein, key hamesha same banegi
        var pairKey = string.CompareOrdinal(userId1, userId2) < 0
            ? $"{userId1}:{userId2}"
            : $"{userId2}:{userId1}";
        var gate = _directConversationLocks.GetOrAdd(pairKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
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
        finally
        {
            gate.Release();
        }
    }

    public async Task<List<InternalMessageDto>> GetMessagesAsync(int conversationId)
    {
        var participants = await _dbContext.ConversationParticipants
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync();

        var messages = await _dbContext.InternalMessages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var result = new List<InternalMessageDto>();
        foreach (var m in messages)
        {
            var others = participants.Where(p => p.UserId != m.SenderId).ToList();

            DateTime? seenByAllAt = null;
            if (others.Count > 0 && others.All(p => p.LastReadAt.HasValue && p.LastReadAt.Value >= m.SentAt))
            {
                seenByAllAt = others.Max(p => p.LastReadAt!.Value);
            }

            result.Add(new InternalMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.FullName,
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = seenByAllAt
            });
        }

        return result;
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
        var participant = await _dbContext.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == readerUserId);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<ParticipantDto>> GetParticipantsAsync(int conversationId)
    {
        return await _dbContext.ConversationParticipants
            .Include(p => p.User)
            .Where(p => p.ConversationId == conversationId)
            .Select(p => new ParticipantDto
            {
                UserId = p.UserId,
                FullName = p.User.FullName,
                LastReadAt = p.LastReadAt
            })
            .ToListAsync();
    }
    public async Task<List<ParticipantDto>> GetContactsAsync(string currentUserId)
    {
        return await _dbContext.Users
            .Where(u => (u.Role == Domain.Enums.UserRole.Admin || u.Role == Domain.Enums.UserRole.Staff)
                        && u.Id != currentUserId)
            .OrderBy(u => u.FullName)
            .Select(u => new ParticipantDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                LastReadAt = null
            })
            .ToListAsync();
    }
}